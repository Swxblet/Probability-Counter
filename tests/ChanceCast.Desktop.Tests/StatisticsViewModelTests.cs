using System.Globalization;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Tests;

public class StatisticsViewModelTests
{
    private static StatisticsViewModel Create(AppState state)
    {
        var (service, _, _, _, _) = ViewModelFactory.Create(state);
        return new StatisticsViewModel(service);
    }

    [Fact]
    public void EmptyHistory_ShowsNoData()
    {
        var vm = Create(AppState.CreateDefault());

        Assert.Equal("0", vm.MatchesText);
        Assert.Equal("0", vm.TotalAttemptsText);
        Assert.Equal("Sin datos", vm.AverageText);
        Assert.Equal("1 entre " + Core.Services.ProbabilityFormatter.FormatLong(700_000), vm.RateText);
        Assert.All(vm.Targets, t => Assert.Equal("Sin datos", t.ProjectionText));
    }

    [Fact]
    public void WithMatches_ShowsTotalsAndAverage()
    {
        var vm = Create(new AppState
        {
            Matches = new[]
            {
                new CompletedMatch(Guid.NewGuid(), DateTimeOffset.Now.AddHours(-2), DateTimeOffset.Now.AddHours(-1), 10),
                new CompletedMatch(Guid.NewGuid(), DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, 20),
            },
        });

        Assert.Equal("2", vm.MatchesText);
        Assert.Equal("30", vm.TotalAttemptsText);
        Assert.Equal("15", vm.AverageText.Replace(",", ".").Replace(" ", ""));
    }

    [Fact]
    public void WithCustomDenominator_UsesCurrentSettings()
    {
        var vm = Create(new AppState
        {
            Settings = AppSettings.CreateDefault() with { DropDenominator = 2 },
            Matches = new[] { new CompletedMatch(Guid.NewGuid(), DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, 3) },
        });

        Assert.Equal("1 entre 2", vm.RateText);
        Assert.Equal("87,5 %".Replace(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator),
            vm.HistoryProbabilityText);
    }

    [Fact]
    public void Disclaimers_ArePresent()
    {
        Assert.Contains("independientes", StatisticsViewModel.IndependenceDisclaimer);
        Assert.Contains("no aumenta la probabilidad", StatisticsViewModel.IndependenceDisclaimer);
        Assert.Contains("no confirma", StatisticsViewModel.HistoryDisclaimer);
        Assert.Contains("hasta un éxito", StatisticsViewModel.TheoreticalAverageLabel);
    }

    [Fact]
    public void Refresh_PicksUpServiceChanges()
    {
        var (service, _, _, _, _) = ViewModelFactory.Create();
        var vm = new StatisticsViewModel(service);
        Assert.Equal("0", vm.MatchesText);

        service.Initialize(new AppState
        {
            Matches = new[] { new CompletedMatch(Guid.NewGuid(), DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, 7) },
        });

        Assert.Equal("1", vm.MatchesText);
        Assert.Equal("7", vm.TotalAttemptsText);
    }
}
