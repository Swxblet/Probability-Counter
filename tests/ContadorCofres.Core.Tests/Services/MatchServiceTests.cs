using ContadorCofres.Core.Abstractions;
using ContadorCofres.Core.Models;
using ContadorCofres.Core.Services;

namespace ContadorCofres.Core.Tests.Services;

public class MatchServiceTests
{
    private static (MatchService Service, InMemoryStore Store, FakeTimeProvider Clock) Create()
    {
        var store = new InMemoryStore();
        var clock = new FakeTimeProvider();
        var service = new MatchService(store, NullLogger.Instance, clock);
        service.Initialize(AppState.CreateDefault());
        return (service, store, clock);
    }

    // ---- Iniciar ----

    [Fact]
    public async Task StartMatch_CreatesActiveMatchWithZeroCount()
    {
        var (service, store, _) = Create();

        var result = await service.StartMatchAsync();

        Assert.True(result.Success);
        var active = service.Current.ActiveMatch;
        Assert.NotNull(active);
        Assert.Equal(0, active.ChestCount);
        Assert.NotEqual(Guid.Empty, active.Id);
        Assert.Equal(active, store.SavedState!.ActiveMatch);
    }

    [Fact]
    public async Task StartMatch_WhenAlreadyActive_Fails()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();

        var result = await service.StartMatchAsync();

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
        Assert.NotNull(service.Current.ActiveMatch);
    }

    // ---- Sumar / Restar ----

    [Fact]
    public async Task Increment_WithoutActiveMatch_Fails()
    {
        var (service, store, _) = Create();

        var result = await service.IncrementAsync();

        Assert.False(result.Success);
        Assert.Equal(0, store.SaveCalls);
    }

    [Fact]
    public async Task Increment_IncreasesCountAndSaves()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();

        var result = await service.IncrementAsync();

        Assert.True(result.Success);
        Assert.Equal(1, service.Current.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task Increment_AtMaxValue_Fails()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.SetCountAsync(long.MaxValue);

        var result = await service.IncrementAsync();

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
        Assert.Equal(long.MaxValue, service.Current.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task Decrement_AtZero_Fails()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();

        var result = await service.DecrementAsync();

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
        Assert.Equal(0, service.Current.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task Decrement_WithoutActiveMatch_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.DecrementAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Decrement_AndIncrement_RoundTrip()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.IncrementAsync();
        await service.IncrementAsync();

        await service.DecrementAsync();

        Assert.Equal(1, service.Current.ActiveMatch!.ChestCount);
    }

    // ---- Editar total ----

    [Fact]
    public async Task SetCount_UpdatesTheCount()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.IncrementAsync();

        var result = await service.SetCountAsync(42);

        Assert.True(result.Success);
        Assert.Equal(42, service.Current.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task SetCount_Negative_Fails()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();

        var result = await service.SetCountAsync(-3);

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
        Assert.Equal(0, service.Current.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task SetCount_WithoutActiveMatch_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.SetCountAsync(5);

        Assert.False(result.Success);
    }

    // ---- Finalizar ----

    [Fact]
    public async Task Finish_WithoutActiveMatch_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.FinishMatchAsync();

        Assert.False(result.Success);
    }

    [Fact]
    public async Task Finish_MovesActiveMatchToHistoryWithSameId()
    {
        var (service, store, clock) = Create();
        clock.Set(TestData.BaseStart);
        await service.StartMatchAsync();
        await service.SetCountAsync(12);
        clock.Advance(TimeSpan.FromMinutes(30));

        var result = await service.FinishMatchAsync();

        Assert.True(result.Success);
        Assert.Null(service.Current.ActiveMatch);
        var historyMatch = Assert.Single(service.Current.Matches);
        Assert.Equal(12, historyMatch.ChestCount);
        Assert.Equal(result.FinishedMatch, historyMatch);
        Assert.Null(store.SavedState!.ActiveMatch);
        Assert.Single(store.SavedState.Matches);
    }

    [Fact]
    public async Task Finish_SecondCallFailsWithoutDuplicating()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.FinishMatchAsync();

        var result = await service.FinishMatchAsync();

        Assert.False(result.Success);
        Assert.Single(service.Current.Matches);
    }

    [Fact]
    public async Task Finish_WithZeroChests_SucceedsAndIsIncludedInHistory()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();

        var result = await service.FinishMatchAsync();

        Assert.True(result.Success);
        Assert.Equal(0, Assert.Single(service.Current.Matches).ChestCount);
    }

    [Fact]
    public async Task Finish_ClampsFinishedAtWhenClockGoesBackwards()
    {
        var (service, _, clock) = Create();
        clock.Set(TestData.BaseStart);
        await service.StartMatchAsync();

        clock.Set(TestData.BaseStart.AddHours(-2)); // reloj hacia atrás
        await service.FinishMatchAsync();

        var match = Assert.Single(service.Current.Matches);
        Assert.Equal(match.StartedAt, match.FinishedAt);
    }

    [Fact]
    public async Task Finish_DoesNotStartAnotherMatchAutomatically()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.FinishMatchAsync();

        Assert.Null(service.Current.ActiveMatch);
    }

    // ---- Descartar ----

    [Fact]
    public async Task Discard_RemovesActiveWithoutCreatingHistory()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.SetCountAsync(8);

        var result = await service.DiscardMatchAsync();

        Assert.True(result.Success);
        Assert.Null(service.Current.ActiveMatch);
        Assert.Empty(service.Current.Matches);
    }

    [Fact]
    public async Task Discard_DoesNotTouchPreviousHistory()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.FinishMatchAsync();
        await service.StartMatchAsync();

        await service.DiscardMatchAsync();

        Assert.Single(service.Current.Matches);
    }

    [Fact]
    public async Task Discard_WithoutActiveMatch_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.DiscardMatchAsync();

        Assert.False(result.Success);
    }

    // ---- Editar y eliminar historial ----

    [Fact]
    public async Task EditHistory_UpdatesCount()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.SetCountAsync(4);
        await service.FinishMatchAsync();
        var id = service.Current.Matches[0].Id;

        var result = await service.EditHistoryAsync(id, 20);

        Assert.True(result.Success);
        Assert.Equal(20, service.Current.Matches[0].ChestCount);
    }

    [Fact]
    public async Task EditHistory_UnknownId_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.EditHistoryAsync(Guid.NewGuid(), 5);

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
    }

    [Fact]
    public async Task EditHistory_NegativeCount_Fails()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.FinishMatchAsync();

        var result = await service.EditHistoryAsync(service.Current.Matches[0].Id, -1);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteHistory_RemovesTheRecord()
    {
        var (service, _, _) = Create();
        await service.StartMatchAsync();
        await service.FinishMatchAsync();
        var id = service.Current.Matches[0].Id;

        var result = await service.DeleteHistoryAsync(id);

        Assert.True(result.Success);
        Assert.Empty(service.Current.Matches);
    }

    [Fact]
    public async Task DeleteHistory_UnknownId_Fails()
    {
        var (service, _, _) = Create();

        var result = await service.DeleteHistoryAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    // ---- Configuración ----

    [Fact]
    public async Task UpdateSettings_AppliesNewSettings()
    {
        var (service, store, _) = Create();
        var settings = AppSettings.CreateDefault() with { ItemName = "Paraguas", DropDenominator = 500 };

        var result = await service.UpdateSettingsAsync(settings);

        Assert.True(result.Success);
        Assert.Equal(settings, service.Current.Settings);
        Assert.Equal(settings, store.SavedState!.Settings);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateSettings_InvalidDenominator_Fails(long denominator)
    {
        var (service, _, _) = Create();

        var result = await service.UpdateSettingsAsync(AppSettings.CreateDefault() with { DropDenominator = denominator });

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Validation, result.Error);
        Assert.NotEqual(denominator, service.Current.Settings.DropDenominator);
    }

    // ---- Fallos de guardado ----

    [Fact]
    public async Task SaveFailure_DoesNotConfirmInMemoryState()
    {
        var (service, store, _) = Create();
        await service.StartMatchAsync();
        await service.SetCountAsync(5);

        store.FailNextSave = true;
        var result = await service.IncrementAsync();

        Assert.False(result.Success);
        Assert.Equal(MatchOperationError.Save, result.Error);
        Assert.Equal(5, service.Current.ActiveMatch!.ChestCount);
        Assert.Equal(5, store.SavedState!.ActiveMatch!.ChestCount);
    }

    [Fact]
    public async Task SaveFailure_CanBeRetried()
    {
        var (service, store, _) = Create();
        await service.StartMatchAsync();
        store.FailNextSave = true;
        await service.IncrementAsync(); // falla

        var result = await service.IncrementAsync(); // reintento

        Assert.True(result.Success);
        Assert.Equal(1, service.Current.ActiveMatch!.ChestCount);
    }

    // ---- Concurrencia y eventos ----

    [Fact]
    public async Task ConcurrentOperations_AreSerialized()
    {
        var (service, store, _) = Create();
        await service.StartMatchAsync();

        await Task.WhenAll(service.IncrementAsync(), service.IncrementAsync(), service.IncrementAsync());

        Assert.Equal(3, service.Current.ActiveMatch!.ChestCount);
        Assert.Equal(4, store.SaveCalls);
    }

    [Fact]
    public async Task StateChanged_RaisesOnSuccessButNotOnFailure()
    {
        var (service, _, _) = Create();
        var raised = 0;
        service.StateChanged += (_, _) => raised++;

        var failed = await service.IncrementAsync(); // sin partida activa
        Assert.False(failed.Success);
        Assert.Equal(0, raised);

        await service.StartMatchAsync();
        Assert.Equal(1, raised);
    }
}
