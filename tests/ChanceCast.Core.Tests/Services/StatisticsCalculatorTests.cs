using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.Core.Tests.Services;

public class StatisticsCalculatorTests
{
    [Fact]
    public void Calculate_NullStateThrows()
        => Assert.Throws<ArgumentNullException>(() => StatisticsCalculator.Calculate(null!));

    [Fact]
    public void Calculate_InvalidSettingsThrow()
    {
        var state = new AppState { Settings = AppSettings.CreateDefault() with { DropDenominator = 0 } };
        Assert.Throws<InvalidDataException>(() => StatisticsCalculator.Calculate(state));
    }

    [Fact]
    public void Calculate_EmptyHistory()
    {
        var snapshot = StatisticsCalculator.Calculate(AppState.CreateDefault());

        Assert.Equal(0, snapshot.CompletedMatches);
        Assert.Equal(0, snapshot.TotalAttempts);
        Assert.Null(snapshot.AverageAttemptsPerMatch);
        Assert.Equal(0.0, snapshot.HistoryProbability);
        Assert.Equal(0.0, snapshot.CurrentMatchProbability);
        Assert.Equal(700_000, snapshot.TheoreticalAverageAttempts);
        Assert.Equal(4, snapshot.Targets.Count);
        Assert.All(snapshot.Targets, t =>
        {
            Assert.NotNull(t.AttemptsRequired);
            Assert.Equal(ProjectionKind.NoData, t.Projection.Kind);
        });
    }

    [Fact]
    public void Calculate_TotalsAndAverage()
    {
        var state = TestData.StateWithMatches(10, 15);
        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(2, snapshot.CompletedMatches);
        Assert.Equal(25, snapshot.TotalAttempts);
        Assert.Equal(12.5, snapshot.AverageAttemptsPerMatch);
    }

    [Fact]
    public void Calculate_ZeroAttemptMatches_GiveZeroAverageAndNotEstimableProjections()
    {
        var state = TestData.StateWithMatches(0, 0);
        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(2, snapshot.CompletedMatches);
        Assert.Equal(0, snapshot.TotalAttempts);
        Assert.Equal(0.0, snapshot.AverageAttemptsPerMatch);
        Assert.All(snapshot.Targets, t => Assert.Equal(ProjectionKind.NotEstimable, t.Projection.Kind));
    }

    [Fact]
    public void Calculate_ActiveMatchIsExcludedFromHistoryButHasItsOwnProbability()
    {
        var state = new AppState
        {
            ActiveMatch = new ActiveMatch(Guid.NewGuid(), TestData.BaseStart, 999),
            Matches = [TestData.Match(5)],
        };

        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(1, snapshot.CompletedMatches);
        Assert.Equal(5, snapshot.TotalAttempts);
        Assert.Equal(
            ProbabilityCalculator.AtLeastOneSuccess(999, 700_000),
            snapshot.CurrentMatchProbability);
        Assert.Equal(
            ProbabilityCalculator.AtLeastOneSuccess(5, 700_000),
            snapshot.HistoryProbability);
    }

    [Fact]
    public void Calculate_DenominatorChangeAffectsAllValues()
    {
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { DropDenominator = 2 },
            Matches = [TestData.Match(3)],
        };

        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(2, snapshot.TheoreticalAverageAttempts);
        Assert.Equal(0.875, snapshot.HistoryProbability, 12);
    }

    [Fact]
    public void Calculate_TargetsAndProjections_KnownValues()
    {
        // Con p = 1/2 y un promedio de 2 intentos por sesión:
        // 50 % -> 1 intento -> 1 sesión; 90 % -> 4 intentos -> 2; 95 % -> 5 -> 3; 99 % -> 7 -> 4.
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { DropDenominator = 2 },
            Matches = [TestData.Match(2)],
        };

        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(0.50, snapshot.Targets[0].Target);
        Assert.Equal(1, snapshot.Targets[0].AttemptsRequired);
        Assert.Equal(ProjectionKind.Value, snapshot.Targets[0].Projection.Kind);
        Assert.Equal(1, snapshot.Targets[0].Projection.Matches);

        Assert.Equal(4, snapshot.Targets[1].AttemptsRequired);
        Assert.Equal(2, snapshot.Targets[1].Projection.Matches);

        Assert.Equal(5, snapshot.Targets[2].AttemptsRequired);
        Assert.Equal(3, snapshot.Targets[2].Projection.Matches);

        Assert.Equal(7, snapshot.Targets[3].AttemptsRequired);
        Assert.Equal(4, snapshot.Targets[3].Projection.Matches);
    }

    [Fact]
    public void Calculate_OutOfRangeAttemptsGiveOutOfRangeProjection()
    {
        // Con denominador long.MaxValue el 50 % aún cabe en long (~6.4e18),
        // pero 90/95/99 % requieren > 9.2e18 intentos: fuera de rango.
        // (Ningún denominador long puede desbordar ya el 50 %.)
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { DropDenominator = long.MaxValue },
            Matches = [TestData.Match(5)],
        };

        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.NotNull(snapshot.Targets[0].AttemptsRequired);
        Assert.Equal(ProjectionKind.Value, snapshot.Targets[0].Projection.Kind);

        foreach (var target in snapshot.Targets.Skip(1))
        {
            Assert.Null(target.AttemptsRequired);
            Assert.Equal(ProjectionKind.OutOfRange, target.Projection.Kind);
        }
    }

    [Fact]
    public void Calculate_FractionalAveragesRoundProjectionUp()
    {
        // Promedio 0.5 intentos por sesión: para el 90 % hacen falta 4 intentos -> 8 sesiones.
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { DropDenominator = 2 },
            Matches = [TestData.Match(0), TestData.Match(1)],
        };

        var snapshot = StatisticsCalculator.Calculate(state);

        Assert.Equal(0.5, snapshot.AverageAttemptsPerMatch);
        Assert.Equal(8, snapshot.Targets[1].Projection.Matches);
    }

    [Fact]
    public void Calculate_TotalOverflowThrows()
    {
        var state = new AppState
        {
            Matches = [TestData.Match(long.MaxValue), TestData.Match(1)],
        };

        Assert.Throws<OverflowException>(() => StatisticsCalculator.Calculate(state));
    }
}
