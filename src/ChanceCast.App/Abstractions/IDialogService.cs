using ChanceCast.Core.Models;

namespace ChanceCast.App.Abstractions;

/// <summary>Datos para la ventana de resumen tras finalizar una sesión.</summary>
public sealed record MatchSummaryData(
    long Attempts,
    double MatchProbability,
    long TotalAttempts,
    double? AveragePerMatch,
    string EventName,
    long DropDenominator);

/// <summary>
/// Abstracción de diálogos para que los ViewModels no dependan de WPF.
/// La implementación vive en el proyecto Desktop.
/// </summary>
public interface IDialogService
{
    Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Aceptar");

    void ShowError(string message);

    void ShowInfo(string title, string message);

    /// <summary>Pide un conteo. Devuelve null si el usuario cancela.</summary>
    Task<long?> PromptAttemptsAsync(string title, string label, long current);

    /// <summary>Ruta elegida para exportar. Devuelve null si el usuario cancela (no es un error).</summary>
    Task<string?> GetCsvSavePathAsync(string suggestedFileName);

    /// <summary>Muestra el resumen. Devuelve true si el usuario pidió iniciar la siguiente sesión.</summary>
    Task<bool> ShowSummaryAsync(MatchSummaryData data);

    /// <summary>Muestra la configuración. Devuelve null si el usuario canceló.</summary>
    Task<AppSettings?> ShowSettingsAsync(AppSettings current);
}
