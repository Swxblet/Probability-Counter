using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Abstractions;

/// <summary>Resultado de la carga inicial del estado.</summary>
public enum LoadStatus
{
    /// <summary>No había datos previos (primer inicio).</summary>
    NoData,

    /// <summary>Datos leídos del archivo principal.</summary>
    Loaded,

    /// <summary>El principal estaba dañado o ausente y se restauró la copia de seguridad.</summary>
    RecoveredFromBackup,

    /// <summary>El archivo usa una versión de esquema más reciente; no se sobrescribirá.</summary>
    IncompatibleSchema,
}

/// <summary>Resultado de <see cref="IAppStateStore.Load"/> con el estado y un mensaje legible.</summary>
public sealed record AppStateLoadResult(
    LoadStatus Status,
    AppState? State,
    string? Message = null,
    bool FilesExisted = false)
{
    public static AppStateLoadResult Loaded(AppState state) => new(LoadStatus.Loaded, state);
    public static AppStateLoadResult Recovered(AppState state, string message) => new(LoadStatus.RecoveredFromBackup, state, message);
    public static AppStateLoadResult NoData(string message, bool filesExisted) => new(LoadStatus.NoData, null, message, filesExisted);
    public static AppStateLoadResult Incompatible(string message) => new(LoadStatus.IncompatibleSchema, null, message, FilesExisted: true);
}

/// <summary>Resultado de un guardado.</summary>
public sealed record SaveResult(bool Success, string? ErrorMessage = null)
{
    public static SaveResult Ok() => new(true);
    public static SaveResult Fail(string message) => new(false, message);
}

/// <summary>
/// Almacenamiento del estado de la aplicación. Las implementaciones deben
/// garantizar que un guardado fallido no deje archivos inconsistentes.
/// </summary>
public interface IAppStateStore
{
    AppStateLoadResult Load();

    Task<SaveResult> SaveAsync(AppState state, CancellationToken cancellationToken = default);
}
