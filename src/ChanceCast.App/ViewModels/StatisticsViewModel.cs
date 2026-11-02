using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using ChanceCast.Core.Services;

namespace ChanceCast.App.ViewModels;

/// <summary>Fila de la tabla de objetivos de probabilidad.</summary>
public sealed class TargetRowViewModel
{
    public required string TargetText { get; init; }
    public required string AttemptsText { get; init; }
    public required string ProjectionText { get; init; }
}

/// <summary>
/// Estadísticas de actividad y probabilidad, siempre calculadas desde el
/// historial. Incluye los textos de descargo obligatorios.
/// </summary>
public sealed partial class StatisticsViewModel : ObservableObject
{
    public const string IndependenceDisclaimer =
        "Estas estimaciones suponen intentos independientes y una probabilidad constante. " +
        "El historial no aumenta la probabilidad del siguiente intento.";

    public const string HistoryDisclaimer =
        "Probabilidad teórica de al menos un éxito en ese número de intentos; no confirma si obtuviste el ítem.";

    public const string TheoreticalAverageLabel = "Promedio teórico de intentos hasta un éxito";

    [ObservableProperty]
    private string _matchesText = "0";

    [ObservableProperty]
    private string _totalAttemptsText = "0";

    [ObservableProperty]
    private string _averageText = "Sin datos";

    [ObservableProperty]
    private string _eventNameText = "Evento objetivo";

    [ObservableProperty]
    private string _rateText = "1 entre 700.000";

    [ObservableProperty]
    private string _historyProbabilityText = "0 %";

    [ObservableProperty]
    private string _currentProbabilityText = "0 %";

    [ObservableProperty]
    private string _theoreticalAverageText = "700.000";

    [ObservableProperty]
    private List<TargetRowViewModel> _targets = [];

    public StatisticsViewModel(MatchService service)
    {
        Service = service ?? throw new ArgumentNullException(nameof(service));
        service.StateChanged += (_, _) => Refresh();
        Refresh();
    }

    internal MatchService Service { get; }

    public void Refresh()
    {
        var culture = CultureInfo.CurrentCulture;
        var state = Service.Current;
        var snapshot = StatisticsCalculator.Calculate(state);

        MatchesText = ProbabilityFormatter.FormatLong(snapshot.CompletedMatches, culture);
        TotalAttemptsText = ProbabilityFormatter.FormatLong(snapshot.TotalAttempts, culture);
        AverageText = ProbabilityFormatter.FormatAverage(snapshot.AverageAttemptsPerMatch, culture);
        EventNameText = string.IsNullOrWhiteSpace(state.Settings.EventName) ? "Evento objetivo" : state.Settings.EventName;
        RateText = "1 entre " + ProbabilityFormatter.FormatLong(state.Settings.DropDenominator, culture);
        HistoryProbabilityText = ProbabilityFormatter.FormatPercent(snapshot.HistoryProbability, culture);
        CurrentProbabilityText = ProbabilityFormatter.FormatPercent(snapshot.CurrentMatchProbability, culture);
        TheoreticalAverageText = ProbabilityFormatter.FormatLong(snapshot.TheoreticalAverageAttempts, culture);
        Targets = snapshot.Targets.Select(t => new TargetRowViewModel
        {
            TargetText = ProbabilityFormatter.FormatPercent(t.Target, culture),
            AttemptsText = ProbabilityFormatter.FormatLong(t.AttemptsRequired, culture),
            ProjectionText = ProbabilityFormatter.FormatProjection(t.Projection, culture),
        }).ToList();
    }
}
