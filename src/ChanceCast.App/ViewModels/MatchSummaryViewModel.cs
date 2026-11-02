using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChanceCast.App.Abstractions;
using ChanceCast.Core.Services;

namespace ChanceCast.App.ViewModels;

/// <summary>Resumen tras finalizar una sesión, con opción de iniciar la siguiente.</summary>
public sealed partial class MatchSummaryViewModel : ObservableObject
{
    public MatchSummaryViewModel(MatchSummaryData data, string averageText)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        AverageText = averageText;
    }

    public MatchSummaryData Data { get; }

    public string AverageText { get; }

    public string MatchProbabilityLine =>
        $"Probabilidad de la sesión ({Data.Attempts} {(Data.Attempts == 1 ? "intento" : "intentos")}): " +
        ProbabilityFormatter.FormatPercent(Data.MatchProbability, CultureInfo.CurrentCulture);

    public string TotalText =>
        ProbabilityFormatter.FormatLong(Data.TotalAttempts, CultureInfo.CurrentCulture) + " intentos";

    /// <summary>True si el usuario pidió iniciar la siguiente sesión.</summary>
    public bool StartNextRequested { get; private set; }

    public event EventHandler? RequestClose;

    [RelayCommand]
    private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void StartNext()
    {
        StartNextRequested = true;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
