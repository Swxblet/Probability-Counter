using ContadorCofres.Core.Models;

namespace ContadorCofres.App.Abstractions;

/// <summary>Datos para la ventana de resumen tras finalizar una partida.</summary>
public sealed record MatchSummaryData(
    long ChestCount,
    double MatchProbability,
    long TotalChests,
    double? AveragePerMatch,
    string ItemName,
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
    Task<long?> PromptChestCountAsync(string title, string label, long current);

    /// <summary>Ruta elegida para exportar. Devuelve null si el usuario cancela (no es un error).</summary>
    Task<string?> GetCsvSavePathAsync(string suggestedFileName);

    /// <summary>Muestra el resumen. Devuelve true si el usuario pidió iniciar la siguiente partida.</summary>
    Task<bool> ShowSummaryAsync(MatchSummaryData data);

    /// <summary>Muestra la configuración. Devuelve null si el usuario canceló.</summary>
    Task<AppSettings?> ShowSettingsAsync(AppSettings current);
}
