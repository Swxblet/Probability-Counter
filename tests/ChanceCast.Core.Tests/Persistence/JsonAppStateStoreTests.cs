using System.Text;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Infrastructure;
using ChanceCast.Core.Models;

namespace ChanceCast.Core.Tests.Persistence;

public class JsonAppStateStoreTests : IDisposable
{
    private readonly TempDir _dir = new();

    private string Primary => Path.Combine(_dir.Path, "data.json");
    private string Backup => Path.Combine(_dir.Path, "data.backup.json");

    private JsonAppStateStore CreateStore(string? primaryOverride = null, string? backupOverride = null)
        => new(primaryOverride ?? Primary, backupOverride ?? Backup, NullLogger.Instance);

    public void Dispose() => _dir.Dispose();

    private static AppState SampleState() => new()
    {
        Settings = AppSettings.CreateDefault() with
        {
            EventName = "Pico decorativo",
            DropDenominator = 1_000_000,
            AlwaysOnTop = true,
            ConfirmFinish = false,
            EnableGlobalHotkeys = true,
            IncrementHotkey = new HotkeyBinding("F8", HotkeyModifiers.Control | HotkeyModifiers.Alt),
            DecrementHotkey = null,
        },
        ActiveMatch = new ActiveMatch(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-3)),
            7),
        Matches =
        [
            TestData.Match(5),
            TestData.Match(0),
        ],
    };

    private static void WriteFile(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    // ---- Carga ----

    [Fact]
    public void Load_WithoutFiles_ReturnsNoDataForFirstRun()
    {
        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
        Assert.Null(result.State);
        Assert.False(result.FilesExisted);
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTripsAllFields()
    {
        var state = SampleState();
        var store = CreateStore();
        Assert.True((await store.SaveAsync(state)).Success);

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        var loaded = result.State!;
        Assert.Equal(state.Settings, loaded.Settings);
        Assert.Equal(state.ActiveMatch, loaded.ActiveMatch);
        Assert.Equal(state.Matches, loaded.Matches);
    }

    [Fact]
    public async Task Save_WritesCamelCaseJsonWithSchemaVersion()
    {
        Assert.True((await CreateStore().SaveAsync(SampleState())).Success);

        var json = await File.ReadAllTextAsync(Primary);
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"dropDenominator\"", json);
        Assert.Contains("\"activeMatch\"", json);
        Assert.Contains("\"matches\"", json);
        Assert.Contains("\"theme\": \"Dark\"", json);
    }

    [Fact]
    public async Task Save_ThenLoad_RoundTripsTheme()
    {
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { Theme = AppTheme.Light },
        };

        var store = CreateStore();
        Assert.True((await store.SaveAsync(state)).Success);
        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(AppTheme.Light, result.State!.Settings.Theme);
    }

    [Fact]
    public void Load_MissingTheme_FallsBackToDark()
    {
        // Archivos escritos antes del campo theme.
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null,\"matches\":[]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        Assert.Equal(AppTheme.Dark, result.State!.Settings.Theme);
    }

    [Fact]
    public void Load_InvalidThemeValue_RejectsFile()
    {
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true,\"theme\":\"Sepia\"}," +
            "\"activeMatch\":null,\"matches\":[]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
    }

    [Fact]
    public async Task Save_KeepsPreviousStateAsBackup_AndCorruptPrimaryIsRecovered()
    {
        var first = TestData.StateWithMatches(4);
        var second = TestData.StateWithMatches(4, 9);

        var store = CreateStore();
        Assert.True((await store.SaveAsync(first)).Success);
        Assert.True((await store.SaveAsync(second)).Success);
        Assert.True(File.Exists(Backup));

        // Se daña el archivo principal: la copia de seguridad contiene el primer estado.
        WriteFile(Primary, "{ \"schemaVersion\": 1, \"settings\"");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.RecoveredFromBackup, result.Status);
        Assert.NotNull(result.State);
        Assert.Equal(first.Matches, result.State!.Matches);
        Assert.True(File.Exists(Primary + ".corrupt"), "El archivo dañado debe conservarse.");
    }

    [Fact]
    public async Task Load_PrimaryMissing_BackupValid_IsRecovered()
    {
        var state = TestData.StateWithMatches(4);
        WriteFile(Primary, "{}"); // existe para que se pueda reemplazar
        var store = CreateStore();
        await store.SaveAsync(state);
        await store.SaveAsync(TestData.StateWithMatches(4, 9)); // backup = primer estado
        File.Delete(Primary);

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.RecoveredFromBackup, result.Status);
        Assert.Equal(state.Matches, result.State!.Matches);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"activeMatch\":null,\"matches\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":{\"eventName\":\"x\",\"alwaysOnTop\":false,\"confirmFinish\":true},\"activeMatch\":null,\"matches\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":{\"eventName\":\"x\",\"dropDenominator\":\"abc\",\"alwaysOnTop\":false,\"confirmFinish\":true},\"activeMatch\":null,\"matches\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true},\"activeMatch\":{\"id\":\"" + "00000000-0000-0000-0000-000000000000" + "\",\"startedAt\":\"2026-01-01T10:00:00-03:00\",\"attempts\":-1},\"matches\":[]}")]
    public void Load_InvalidPrimary_WithoutBackup_ReturnsNoDataAndPreservesFile(string json)
    {
        WriteFile(Primary, json);

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
        Assert.True(result.FilesExisted);
        Assert.True(File.Exists(Primary), "El archivo inválido debe conservarse sin cambios.");
        Assert.Equal(json, File.ReadAllText(Primary));
    }

    [Fact]
    public async Task Load_MissingRequiredField_WithValidBackup_IsRecovered()
    {
        var state = TestData.StateWithMatches(3);
        var store = CreateStore();
        await store.SaveAsync(state);
        await store.SaveAsync(TestData.StateWithMatches(3, 5)); // crea la copia de seguridad

        // Se sobrescribe el principal con un JSON al que le falta el historial.
        WriteFile(Primary, "{\"schemaVersion\":1,\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true},\"activeMatch\":null}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.RecoveredFromBackup, result.Status);
        Assert.Equal(state.Matches, result.State!.Matches);
    }

    [Fact]
    public async Task Load_InvalidDate_FallsBackToBackup()
    {
        var state = TestData.StateWithMatches(3);
        var store = CreateStore();
        await store.SaveAsync(state);
        await store.SaveAsync(TestData.StateWithMatches(3, 5)); // crea la copia de seguridad

        var id = Guid.NewGuid();
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null," +
            "\"matches\":[{\"id\":\"" + id + "\",\"startedAt\":\"no-es-una-fecha\",\"finishedAt\":\"2026-01-01T10:20:00-03:00\",\"attempts\":5}]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.RecoveredFromBackup, result.Status);
    }

    [Fact]
    public void Load_DuplicateIds_AreRejected()
    {
        var id = Guid.NewGuid();
        var line = "{\"id\":\"" + id + "\",\"startedAt\":\"2026-01-01T10:00:00-03:00\",\"finishedAt\":\"2026-01-01T10:20:00-03:00\",\"attempts\":5}";
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null," +
            "\"matches\":[" + line + "," + line + "]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
        Assert.True(result.FilesExisted);
    }

    [Fact]
    public void Load_FinishedBeforeStarted_IsRejected()
    {
        var id = Guid.NewGuid();
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null," +
            "\"matches\":[{\"id\":\"" + id + "\",\"startedAt\":\"2026-01-01T10:00:00-03:00\",\"finishedAt\":\"2026-01-01T09:00:00-03:00\",\"attempts\":5}]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
    }

    [Fact]
    public async Task Load_FutureSchema_RejectsAndLocksWrites()
    {
        var original =
            "{\"schemaVersion\":99," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null,\"matches\":[]}";
        WriteFile(Primary, original);

        var store = CreateStore();
        var result = store.Load();

        Assert.Equal(LoadStatus.IncompatibleSchema, result.Status);
        Assert.Null(result.State);

        // No se sobrescriben datos no comprendidos.
        var save = await store.SaveAsync(TestData.StateWithMatches(1));
        Assert.False(save.Success);
        Assert.Equal(original, File.ReadAllText(Primary));
    }

    [Fact]
    public void Load_BothFilesInvalid_ReturnsNoDataAndPreservesCorruptCopy()
    {
        WriteFile(Primary, "contenido corrupto");
        WriteFile(Backup, "también corrupto");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.NoData, result.Status);
        Assert.True(result.FilesExisted);
        Assert.True(File.Exists(Primary));
        Assert.True(File.Exists(Primary + ".corrupt"));
    }

    [Fact]
    public async Task Load_BackupWithFutureSchema_LocksWrites()
    {
        WriteFile(Backup,
            "{\"schemaVersion\":99," +
            "\"settings\":{\"eventName\":\"x\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null,\"matches\":[]}");

        var store = CreateStore();
        var result = store.Load();

        Assert.Equal(LoadStatus.IncompatibleSchema, result.Status);
        Assert.False((await store.SaveAsync(TestData.StateWithMatches(1))).Success);
    }

    [Fact]
    public async Task Load_MinimalV1File_UsesDefaultHotkeys()
    {
        // Archivo v1 sin campos de atajos ni teclas globales (versiones iniciales del esquema).
        WriteFile(Primary,
            "{\"schemaVersion\":1," +
            "\"settings\":{\"eventName\":\"Objetivo\",\"dropDenominator\":700000,\"alwaysOnTop\":false,\"confirmFinish\":true}," +
            "\"activeMatch\":null,\"matches\":[]}");

        var result = CreateStore().Load();

        Assert.Equal(LoadStatus.Loaded, result.Status);
        var settings = result.State!.Settings;
        Assert.Equal("Objetivo", settings.EventName);
        Assert.False(settings.EnableGlobalHotkeys);
        Assert.Equal(AppSettings.CreateDefault().IncrementHotkey, settings.IncrementHotkey);
        Assert.Equal(AppSettings.CreateDefault().StartMatchHotkey, settings.StartMatchHotkey);
    }

    [Fact]
    public async Task Save_WithNonAsciiDirectory_RoundTrips()
    {
        var dir = new TempDir("Carpeta Ñandú");
        try
        {
            var primary = Path.Combine(dir.Path, "data.json");
            var store = new JsonAppStateStore(primary, Path.Combine(dir.Path, "data.backup.json"), NullLogger.Instance);

            Assert.True((await store.SaveAsync(SampleState())).Success);
            var result = new JsonAppStateStore(primary, Path.Combine(dir.Path, "data.backup.json"), NullLogger.Instance).Load();

            Assert.Equal(LoadStatus.Loaded, result.Status);
            Assert.Equal(SampleState().Settings, result.State!.Settings);
        }
        finally
        {
            dir.Dispose();
        }
    }

    [Fact]
    public async Task Save_WhenPrimaryPathIsDirectory_FailsGracefully()
    {
        // Portátil a Windows y Linux: el principal existe como carpeta.
        Directory.CreateDirectory(Primary);
        var store = CreateStore();

        var result = await store.SaveAsync(TestData.StateWithMatches(2, 3));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task Save_WhenPrimaryIsLocked_FailsAndKeepsPreviousState()
    {
        // Solo Windows: renombrar sobre un archivo bloqueado falla.
        // En Linux el rename tiene éxito aunque haya un handle abierto.
        if (!OperatingSystem.IsWindows())
            return;

        var state = TestData.StateWithMatches(2);
        var store = CreateStore();
        Assert.True((await store.SaveAsync(state)).Success);

        using var lockHandle = new FileStream(Primary, FileMode.Open, FileAccess.Read, FileShare.None);
        var result = await store.SaveAsync(TestData.StateWithMatches(2, 3));

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        Assert.False(File.Exists(Primary + ".tmp"), "El archivo temporal debe limpiarse.");
    }

    [Fact]
    public async Task Save_CreatesMissingDirectories()
    {
        var primary = Path.Combine(_dir.Path, "anidada", "carpeta", "data.json");
        var backup = Path.Combine(_dir.Path, "anidada", "carpeta", "data.backup.json");
        var store = CreateStore(primary, backup);

        var result = await store.SaveAsync(TestData.StateWithMatches(1));

        Assert.True(result.Success);
        Assert.True(File.Exists(primary));
    }

    [Fact]
    public void Ctor_SamePathForPrimaryAndBackup_Throws()
        => Assert.Throws<ArgumentException>(() => CreateStore(Primary, Primary));

    [Fact]
    public void Ctor_EmptyPaths_Throw()
    {
        Assert.Throws<ArgumentException>(() => CreateStore("", Backup));
        Assert.Throws<ArgumentException>(() => CreateStore(Primary, " "));
    }

    [Fact]
    public async Task Save_AfterRecovery_FromNoDataWithFiles_OverwritesAndKeepsCorruptCopy()
    {
        WriteFile(Primary, "roto");

        var store = CreateStore();
        var load = store.Load();
        Assert.Equal(LoadStatus.NoData, load.Status);
        Assert.True(load.FilesExisted);

        var saved = await store.SaveAsync(TestData.StateWithMatches(1));

        Assert.True(saved.Success);
        Assert.True(File.Exists(Primary + ".corrupt"), "El archivo dañado debe conservarse.");
        var reloaded = CreateStore().Load();
        Assert.Equal(LoadStatus.Loaded, reloaded.Status);
        Assert.Single(reloaded.State!.Matches);
    }
}
