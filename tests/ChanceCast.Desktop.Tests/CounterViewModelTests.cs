using ChanceCast.App.Abstractions;
using ChanceCast.App.Services;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.Desktop.Tests;

public class CounterViewModelTests
{
    private static (CounterViewModel Vm, MatchService Service, FakeDialogs Dialogs, AppStatus Status) Create()
    {
        var (service, _, dialogs, _, status) = ViewModelFactory.Create();
        return (new CounterViewModel(service, dialogs, status), service, dialogs, status);
    }

    [Fact]
    public void Initial_WithoutActiveMatch_StartEnabledOthersDisabled()
    {
        var (vm, _, _, _) = Create();

        Assert.False(vm.HasActiveMatch);
        Assert.Equal(0, vm.Attempts);
        Assert.True(vm.StartMatchCommand.CanExecute(null));
        Assert.False(vm.IncrementCommand.CanExecute(null));
        Assert.False(vm.DecrementCommand.CanExecute(null));
        Assert.False(vm.FinishMatchCommand.CanExecute(null));
        Assert.False(vm.DiscardMatchCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartMatch_EnablesCounting()
    {
        var (vm, _, _, _) = Create();

        await vm.StartMatchCommand.ExecuteAsync(null);

        Assert.True(vm.HasActiveMatch);
        Assert.True(vm.IncrementCommand.CanExecute(null));
        Assert.False(vm.DecrementCommand.CanExecute(null)); // cero
        Assert.True(vm.FinishMatchCommand.CanExecute(null));
    }

    [Fact]
    public async Task Increment_UpdatesCountAndEnablesDecrement()
    {
        var (vm, _, _, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);

        await vm.IncrementCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.Attempts);
        Assert.True(vm.DecrementCommand.CanExecute(null));
        Assert.NotEqual("0 %", vm.MatchProbabilityText); // probabilidad > 0 tras un intento
    }

    [Fact]
    public async Task Decrement_AtZero_StaysDisabled()
    {
        var (vm, _, _, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);

        Assert.False(vm.DecrementCommand.CanExecute(null));

        await vm.IncrementCommand.ExecuteAsync(null);
        await vm.DecrementCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.Attempts);
        Assert.False(vm.DecrementCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditCount_Cancel_DoesNotModify()
    {
        var (vm, _, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        dialogs.PromptResult = null; // cancelar

        await vm.EditCountCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.Attempts);
    }

    [Fact]
    public async Task EditCount_AcceptsValue()
    {
        var (vm, _, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        dialogs.PromptResult = 25;

        await vm.EditCountCommand.ExecuteAsync(null);

        Assert.Equal(25, vm.Attempts);
    }

    [Fact]
    public async Task Finish_Cancel_DoesNotFinish()
    {
        var (vm, service, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        await vm.IncrementCommand.ExecuteAsync(null);
        dialogs.ConfirmResult = false;

        await vm.FinishMatchCommand.ExecuteAsync(null);

        Assert.NotNull(service.Current.ActiveMatch);
        Assert.Empty(service.Current.Matches);
    }

    [Fact]
    public async Task Finish_WithConfirm_FinishesAndShowsSummary()
    {
        var (vm, service, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        await vm.IncrementCommand.ExecuteAsync(null);
        await vm.IncrementCommand.ExecuteAsync(null);

        await vm.FinishMatchCommand.ExecuteAsync(null);

        Assert.Null(service.Current.ActiveMatch);
        Assert.Single(service.Current.Matches);
        Assert.False(vm.HasActiveMatch);
        Assert.Equal(1, dialogs.ConfirmCalls);
    }

    [Fact]
    public async Task Finish_ZeroAttempts_AsksExplicitConfirmation()
    {
        var (vm, service, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);

        await vm.FinishMatchCommand.ExecuteAsync(null);

        Assert.Equal(1, dialogs.ConfirmCalls);
        Assert.Single(service.Current.Matches);
    }

    [Fact]
    public async Task Finish_SummaryStartNext_StartsNewMatch()
    {
        var (vm, service, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        dialogs.SummaryResult = true;

        await vm.FinishMatchCommand.ExecuteAsync(null);

        Assert.NotNull(service.Current.ActiveMatch);
        Assert.Single(service.Current.Matches);
    }

    [Fact]
    public async Task Discard_Cancel_KeepsMatch()
    {
        var (vm, service, dialogs, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);
        dialogs.ConfirmResult = false;

        await vm.DiscardMatchCommand.ExecuteAsync(null);

        Assert.NotNull(service.Current.ActiveMatch);
    }

    [Fact]
    public async Task Discard_Confirm_RemovesWithoutHistory()
    {
        var (vm, service, _, _) = Create();
        await vm.StartMatchCommand.ExecuteAsync(null);

        await vm.DiscardMatchCommand.ExecuteAsync(null);

        Assert.Null(service.Current.ActiveMatch);
        Assert.Empty(service.Current.Matches);
    }

    [Fact]
    public async Task SaveFailure_ShowsErrorAndKeepsCount()
    {
        var (service, store, dialogs, _, status) = ViewModelFactory.Create();
        var vm = new CounterViewModel(service, dialogs, status);
        await vm.StartMatchCommand.ExecuteAsync(null);
        store.FailNextSave = true;

        await vm.IncrementCommand.ExecuteAsync(null);

        Assert.True(status.IsError);
        Assert.NotEmpty(status.Message);
        Assert.Equal(0, vm.Attempts);
    }

    [Fact]
    public async Task IsBusy_DisablesCommandsDuringOperation()
    {
        var (vm, _, _, _) = Create();

        Assert.True(vm.StartMatchCommand.CanExecute(null));
        vm.IsBusy = true;

        Assert.False(vm.StartMatchCommand.CanExecute(null));
        Assert.False(vm.IncrementCommand.CanExecute(null));
    }
}
