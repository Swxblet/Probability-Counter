using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Services;

/// <summary>Estado de una proyección de partidas para un objetivo.</summary>
public enum ProjectionKind
{
    /// <summary>Hay un valor calculado.</summary>
    Value,

    /// <summary>No hay partidas en el historial ("Sin datos").</summary>
    NoData,

    /// <summary>El promedio observado es cero ("No estimable").</summary>
    NotEstimable,

    /// <summary>El cálculo excede el rango soportado ("Fuera de rango").</summary>
    OutOfRange,
}

public sealed record MatchProjection(ProjectionKind Kind, long? Matches)
{
    public static MatchProjection None { get; } = new(ProjectionKind.NoData, null);
    public static MatchProjection NotEstimable { get; } = new(ProjectionKind.NotEstimable, null);
    public static MatchProjection OutOfRange { get; } = new(ProjectionKind.OutOfRange, null);
    public static MatchProjection From(long value) => new(ProjectionKind.Value, value);
}

/// <summary>Cofres necesarios para un objetivo y partidas aproximadas.</summary>
public sealed record TargetEstimate(double Target, long? AttemptsRequired, MatchProjection Projection);

/// <summary>Estadísticas calculadas a partir del estado; nunca se persisten.</summary>
public sealed record StatisticsSnapshot(
    long CompletedMatches,
    long TotalChests,
    double? AverageChestsPerMatch,
    double HistoryProbability,
    double CurrentMatchProbability,
    long TheoreticalAverageChests,
    IReadOnlyList<TargetEstimate> Targets)
{
    /// <summary>Objetivos estándar mostrados en la pestaña de estadísticas.</summary>
    public static readonly double[] StandardTargets = [0.50, 0.90, 0.95, 0.99];
}

/// <summary>
/// Cálculo de estadísticas de actividad y probabilidad a partir del historial.
/// La partida activa no se incluye en el historial.
/// </summary>
public static class StatisticsCalculator
{
    public static StatisticsSnapshot Calculate(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var settings = state.Settings;
        ModelValidation.ValidateSettings(settings);

        long total = 0;
        foreach (var match in state.Matches)
            checked { total += match.ChestCount; }

        long count = state.Matches.Count;
        double? average = count == 0 ? null : (double)total / count;

        double historyProbability = ProbabilityCalculator.AtLeastOneSuccess(total, settings.DropDenominator);
        double currentProbability = ProbabilityCalculator.AtLeastOneSuccess(state.ActiveMatch?.ChestCount ?? 0, settings.DropDenominator);

        var targets = new List<TargetEstimate>(StatisticsSnapshot.StandardTargets.Length);
        foreach (var target in StatisticsSnapshot.StandardTargets)
        {
            long? attempts = ProbabilityCalculator.AttemptsForTarget(target, settings.DropDenominator);
            targets.Add(new TargetEstimate(target, attempts, ProjectMatches(attempts, count, average)));
        }

        return new StatisticsSnapshot(
            count,
            total,
            average,
            historyProbability,
            currentProbability,
            settings.DropDenominator,
            targets);
    }

    /// <summary>
    /// partidas_objetivo = ceil(n_q / c) usando el promedio observado c.
    /// Es una proyección, no una garantía del número exacto de partidas.
    /// </summary>
    private static MatchProjection ProjectMatches(long? attempts, long matchCount, double? averagePerMatch)
    {
        if (matchCount == 0)
            return MatchProjection.None;
        if (averagePerMatch is not > 0.0)
            return MatchProjection.NotEstimable;
        if (attempts is null)
            return MatchProjection.OutOfRange;

        double raw = Math.Ceiling(attempts.Value / averagePerMatch.Value);
        if (raw < 1.0 || raw >= (double)long.MaxValue)
            return MatchProjection.OutOfRange;

        return MatchProjection.From((long)raw);
    }
}
