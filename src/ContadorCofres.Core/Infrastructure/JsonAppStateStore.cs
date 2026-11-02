using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using ContadorCofres.Core.Abstractions;
using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Infrastructure;

/// <summary>
/// Almacenamiento del estado en JSON con escritura atómica: se escribe un
/// archivo temporal en la misma carpeta, se vacía a disco y se reemplaza el
/// principal con <see cref="File.Replace"/>, conservando el estado anterior
/// como copia de seguridad.
/// </summary>
public sealed class JsonAppStateStore : IAppStateStore
{
    /// <summary>Versión del esquema escrita y soportada por esta aplicación.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
    };

    private readonly string _primaryPath;
    private readonly string _backupPath;
    private readonly string _tempPath;
    private readonly IAppLogger _logger;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    /// <summary>
    /// Se activa cuando el archivo principal usa un esquema más reciente.
    /// Mientras esté activo no se sobrescriben datos no comprendidos.
    /// </summary>
    private bool _writeLocked;

    public JsonAppStateStore(string primaryPath, string backupPath, IAppLogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);
        ArgumentNullException.ThrowIfNull(logger);

        _primaryPath = Path.GetFullPath(primaryPath);
        _backupPath = Path.GetFullPath(backupPath);
        if (string.Equals(_primaryPath, _backupPath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("La copia de seguridad debe ser un archivo distinto del principal.", nameof(backupPath));

        _tempPath = _primaryPath + ".tmp";
        _logger = logger;
    }

    public AppStateLoadResult Load()
    {
        var primaryExisted = File.Exists(_primaryPath);
        var backupExisted = File.Exists(_backupPath);

        if (!primaryExisted && !backupExisted)
            return AppStateLoadResult.NoData("Primer inicio: no hay datos previos.", filesExisted: false);

        if (primaryExisted)
        {
            var (status, state, error) = TryRead(_primaryPath);
            if (status == ReadStatus.Ok)
                return AppStateLoadResult.Loaded(state!);

            if (status == ReadStatus.IncompatibleSchema)
            {
                _writeLocked = true;
                _logger.Error("CargarDatos", error);
                return AppStateLoadResult.Incompatible(
                    "El archivo de datos fue creado con una versión más reciente de la aplicación. " +
                    "Actualizá la aplicación antes de volver a usar estos datos. El archivo no se modificará.");
            }

            _logger.Error("CargarDatos", "El archivo principal no es válido: " + error);
            PreserveCorruptFile();

            if (backupExisted)
            {
                var (backupStatus, backupState, backupError) = TryRead(_backupPath);
                if (backupStatus == ReadStatus.Ok)
                {
                    _logger.Warn("CargarDatos", "Se restauró la copia de seguridad.");
                    return AppStateLoadResult.Recovered(
                        backupState!,
                        "El archivo de datos estaba dañado y se restauró la última copia de seguridad válida. " +
                        "El archivo dañado se conservó como data.json.corrupt.");
                }

                if (backupStatus == ReadStatus.IncompatibleSchema)
                {
                    _writeLocked = true;
                    _logger.Error("CargarDatos", backupError);
                    return AppStateLoadResult.Incompatible(
                        "La copia de seguridad usa una versión más reciente de la aplicación. " +
                        "Actualizá la aplicación antes de volver a usar estos datos. Los archivos no se modificarán.");
                }

                _logger.Error("CargarDatos", "La copia de seguridad tampoco es válida: " + backupError);
            }

            return AppStateLoadResult.NoData(
                "No se pudieron leer los datos ni la copia de seguridad. Ambos archivos se conservan sin cambios.",
                filesExisted: true);
        }

        // El principal no existe pero sí el respaldo (p. ej. borrado manual).
        var (status2, state2, error2) = TryRead(_backupPath);
        if (status2 == ReadStatus.Ok)
            return AppStateLoadResult.Recovered(
                state2!,
                "No se encontró el archivo de datos y se restauró la copia de seguridad.");

        if (status2 == ReadStatus.IncompatibleSchema)
        {
            _writeLocked = true;
            _logger.Error("CargarDatos", error2);
            return AppStateLoadResult.Incompatible(
                "La copia de seguridad usa una versión más reciente de la aplicación. " +
                "Actualizá la aplicación antes de volver a usar estos datos. Los archivos no se modificarán.");
        }

        _logger.Error("CargarDatos", "La copia de seguridad no es válida: " + error2);
        return AppStateLoadResult.NoData(
            "No se pudieron leer los datos. Los archivos presentes se conservan sin cambios.",
            filesExisted: true);
    }

    public async Task<SaveResult> SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (_writeLocked)
        {
            const string lockedMessage =
                "No se guardarán cambios: el archivo de datos usa una versión más reciente de la aplicación.";
            _logger.Error("GuardarDatos", lockedMessage);
            return SaveResult.Fail(lockedMessage);
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(MapToDto(state), SerializeOptions);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.Error("GuardarDatos", "No se pudo serializar el estado.", ex);
            return SaveResult.Fail("No se pudieron guardar los cambios: error interno de serialización.");
        }

        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            var directory = Path.GetDirectoryName(_primaryPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var bytes = Encoding.UTF8.GetBytes(json);
            // La escritura en disco se hace fuera del hilo de interfaz.
            return await Task.Run(() => WriteAtomically(bytes), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return SaveResult.Fail("El guardado fue cancelado.");
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private SaveResult WriteAtomically(object? state)
    {
        var bytes = (byte[])state!;
        try
        {
            using (var temp = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                temp.Write(bytes, 0, bytes.Length);
                temp.Flush(flushToDisk: true);
            }

            if (File.Exists(_primaryPath))
            {
                // El estado principal anterior pasa a ser la copia de seguridad.
                File.Replace(_tempPath, _primaryPath, _backupPath);
            }
            else
            {
                File.Move(_tempPath, _primaryPath);
            }

            return SaveResult.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or NotSupportedException)
        {
            _logger.Error("GuardarDatos", "No se pudo escribir el archivo de datos.", ex);
            try
            {
                if (File.Exists(_tempPath))
                    File.Delete(_tempPath);
            }
            catch (IOException)
            {
                // El archivo temporal se limpia en el próximo guardado.
            }

            return SaveResult.Fail(
                "No se pudieron guardar los cambios. Revisá que la carpeta de datos esté disponible e intentá nuevamente.");
        }
    }

    private void PreserveCorruptFile()
    {
        try
        {
            File.Copy(_primaryPath, _primaryPath + ".corrupt", overwrite: true);
            _logger.Warn("CargarDatos", "Se conservó una copia del archivo dañado como data.json.corrupt.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warn("CargarDatos", "No se pudo conservar una copia del archivo dañado.", ex);
        }
    }

    private enum ReadStatus
    {
        Ok,
        Invalid,
        IncompatibleSchema,
    }

    private (ReadStatus Status, AppState? State, string? Error) TryRead(string path)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<StateDto>(File.ReadAllText(path), DeserializeOptions);
            if (dto is null)
                return (ReadStatus.Invalid, null, "El archivo está vacío.");

            if (dto.SchemaVersion is not int version)
                return (ReadStatus.Invalid, null, "Falta la versión del esquema.");

            if (version > CurrentSchemaVersion)
                return (ReadStatus.IncompatibleSchema, null,
                    $"El archivo usa el esquema {version}, más reciente que el soportado ({CurrentSchemaVersion}).");

            if (version < CurrentSchemaVersion)
                return (ReadStatus.Invalid, null,
                    $"El archivo usa el esquema {version}, que esta versión ya no soporta.");

            var state = MapToState(dto); // valida invariantes; lanza InvalidDataException
            return (ReadStatus.Ok, state, null);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or IOException or FormatException)
        {
            return (ReadStatus.Invalid, null, ex.Message);
        }
    }

    private static AppState MapToState(StateDto dto)
    {
        var settingsDto = dto.Settings ?? throw new InvalidDataException("Falta la configuración.");

        ActiveMatch? active = null;
        if (dto.ActiveMatch is { } activeDto)
        {
            active = new ActiveMatch(
                activeDto.Id ?? throw new InvalidDataException("La partida activa no tiene identificador."),
                activeDto.StartedAt ?? throw new InvalidDataException("Falta la fecha de inicio de la partida activa."),
                activeDto.ChestCount ?? throw new InvalidDataException("Falta el conteo de la partida activa."));
        }

        var matchList = dto.Matches ?? throw new InvalidDataException("Falta el historial de partidas.");
        var matches = new List<CompletedMatch>(matchList.Count);
        foreach (var matchDto in matchList)
        {
            matches.Add(new CompletedMatch(
                matchDto.Id ?? throw new InvalidDataException("Un registro del historial no tiene identificador."),
                matchDto.StartedAt ?? throw new InvalidDataException("Falta la fecha de inicio de un registro."),
                matchDto.FinishedAt ?? throw new InvalidDataException("Falta la fecha de finalización de un registro."),
                matchDto.ChestCount ?? throw new InvalidDataException("Falta el conteo de un registro.")));
        }

        var state = new AppState
        {
            Settings = new AppSettings
            {
                ItemName = settingsDto.ItemName ?? string.Empty,
                DropDenominator = settingsDto.DropDenominator
                    ?? throw new InvalidDataException("Falta el denominador configurado."),
                AlwaysOnTop = settingsDto.AlwaysOnTop
                    ?? throw new InvalidDataException("Falta la opción de siempre visible."),
                ConfirmFinish = settingsDto.ConfirmFinish
                    ?? throw new InvalidDataException("Falta la opción de confirmar finalización."),
                EnableGlobalHotkeys = settingsDto.EnableGlobalHotkeys ?? false,
                IncrementHotkey = settingsDto.IncrementHotkey,
                DecrementHotkey = settingsDto.DecrementHotkey,
                EditCountHotkey = settingsDto.EditCountHotkey,
                StartMatchHotkey = settingsDto.StartMatchHotkey,
            },
            ActiveMatch = active,
            Matches = matches,
        };

        ModelValidation.ValidateSettings(state.Settings);
        ModelValidation.ValidateHotkeyBinding(state.Settings.IncrementHotkey);
        ModelValidation.ValidateHotkeyBinding(state.Settings.DecrementHotkey);
        ModelValidation.ValidateHotkeyBinding(state.Settings.EditCountHotkey);
        ModelValidation.ValidateHotkeyBinding(state.Settings.StartMatchHotkey);
        if (active is not null)
            ModelValidation.ValidateActiveMatch(active);
        ModelValidation.ValidateMatches(matches);

        return state;
    }

    private static StateDto MapToDto(AppState state) => new()
    {
        SchemaVersion = CurrentSchemaVersion,
        Settings = new SettingsDto
        {
            ItemName = state.Settings.ItemName ?? string.Empty,
            DropDenominator = state.Settings.DropDenominator,
            AlwaysOnTop = state.Settings.AlwaysOnTop,
            ConfirmFinish = state.Settings.ConfirmFinish,
            EnableGlobalHotkeys = state.Settings.EnableGlobalHotkeys,
            IncrementHotkey = state.Settings.IncrementHotkey,
            DecrementHotkey = state.Settings.DecrementHotkey,
            EditCountHotkey = state.Settings.EditCountHotkey,
            StartMatchHotkey = state.Settings.StartMatchHotkey,
        },
        ActiveMatch = state.ActiveMatch is null
            ? null
            : new ActiveMatchDto
            {
                Id = state.ActiveMatch.Id,
                StartedAt = state.ActiveMatch.StartedAt,
                ChestCount = state.ActiveMatch.ChestCount,
            },
        Matches = state.Matches.Select(m => new CompletedMatchDto
        {
            Id = m.Id,
            StartedAt = m.StartedAt,
            FinishedAt = m.FinishedAt,
            ChestCount = m.ChestCount,
        }).ToList(),
    };

    // Los DTO son estrictos: los campos obligatorios usan `required`, así que
    // un JSON con campos faltantes o tipos incorrectos se rechaza.
    private sealed class StateDto
    {
        public required int? SchemaVersion { get; set; }
        public required SettingsDto? Settings { get; set; }
        public ActiveMatchDto? ActiveMatch { get; set; }
        public required List<CompletedMatchDto>? Matches { get; set; }
    }

    private sealed class SettingsDto
    {
        public required string? ItemName { get; set; }
        public required long? DropDenominator { get; set; }
        public required bool? AlwaysOnTop { get; set; }
        public required bool? ConfirmFinish { get; set; }
        public bool? EnableGlobalHotkeys { get; set; }

        // Los atajos no son obligatorios: si el campo falta en el JSON se usa
        // el valor por defecto (archivo v1 inicial); un null explícito indica
        // que el usuario quitó el atajo.
        public HotkeyBinding? IncrementHotkey { get; set; } = AppSettings.CreateDefault().IncrementHotkey;
        public HotkeyBinding? DecrementHotkey { get; set; } = AppSettings.CreateDefault().DecrementHotkey;
        public HotkeyBinding? EditCountHotkey { get; set; } = AppSettings.CreateDefault().EditCountHotkey;
        public HotkeyBinding? StartMatchHotkey { get; set; } = AppSettings.CreateDefault().StartMatchHotkey;
    }

    private sealed class ActiveMatchDto
    {
        public required Guid? Id { get; set; }
        public required DateTimeOffset? StartedAt { get; set; }
        public required long? ChestCount { get; set; }
    }

    private sealed class CompletedMatchDto
    {
        public required Guid? Id { get; set; }
        public required DateTimeOffset? StartedAt { get; set; }
        public required DateTimeOffset? FinishedAt { get; set; }
        public required long? ChestCount { get; set; }
    }
}
