using ChanceCast.App.Services;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.Desktop.Tests;

public class MainViewModelTests
{
    private static (MainViewModel Vm, MatchService Service, FakeDialogs Dialogs, FakeHotkeys Hotkeys, FakeThemeManager Themes, AppStatus Status) Create(AppState? initial = null)
    {
        var (service, _, dialogs, hotkeys, status) = ViewModelFactory.Create(initial);
        var themes = new FakeThemeManager();
        var exporter = new App.Services.CsvExportService(dialogs, NullLogger.Instance);
        var vm = new MainViewModel(
            service,
            dialogs,
            hotkeys,
            themes,
            status,
            new CounterViewModel(service, dialogs, status),
            new HistoryViewModel(service, dialogs, exporter, status),
            new StatisticsViewModel(service));
        return (vm, service, dialogs, hotkeys, themes, status);
    }

    [Fact]
    public void ApplyLoadResult_Loaded_SetsOkStatus()
    {
        var (vm, _, _, _, _, status) = Create();
        var state = new AppState
        {
            Matches = new[] { new CompletedMatch(Guid.NewGuid(), DateTimeOffset.Now.AddHours(-1), DateTimeOffset.Now, 4) },
        };

        vm.ApplyLoadResult(AppStateLoadResult.Loaded(state));

        Assert.False(status.IsError);
        Assert.Equal("1", vm.History.Items.Count.ToString());
        Assert.Equal("4", vm.Statistics.TotalAttemptsText);
    }

    [Fact]
    public void ApplyLoadResult_Recovered_ShowsError()
    {
        var (vm, _, _, _, _, status) = Create();

        vm.ApplyLoadResult(AppStateLoadResult.Recovered(AppState.CreateDefault(), "restaurado"));

        Assert.True(status.IsError);
        Assert.Equal("restaurado", status.Message);
    }

    [Fact]
    public void ApplyLoadResult_Incompatible_ShowsErrorAndEmptyState()
    {
        var (vm, service, _, _, _, status) = Create();

        vm.ApplyLoadResult(AppStateLoadResult.Incompatible("versión más reciente"));

        Assert.True(status.IsError);
        Assert.Empty(service.Current.Matches);
        Assert.Null(service.Current.ActiveMatch);
    }

    [Fact]
    public void ApplyLoadResult_NoDataWithFiles_ShowsError()
    {
        var (vm, _, _, _, _, status) = Create();

        vm.ApplyLoadResult(AppStateLoadResult.NoData("archivos ilegibles", filesExisted: true));

        Assert.True(status.IsError);
    }

    [Fact]
    public void ApplyLoadResult_FirstRun_ShowsWelcome()
    {
        var (vm, _, _, _, _, status) = Create();

        vm.ApplyLoadResult(AppStateLoadResult.NoData("primer inicio", filesExisted: false));

        Assert.False(status.IsError);
        Assert.Contains("primera sesión", status.Message);
    }

    [Fact]
    public async Task OpenSettings_Cancel_DoesNotModify()
    {
        var (vm, service, dialogs, _, _, _) = Create();
        dialogs.SettingsResult = null;

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(AppSettings.CreateDefault().DropDenominator, service.Current.Settings.DropDenominator);
    }

    [Fact]
    public async Task OpenSettings_Save_AppliesSettings()
    {
        var (vm, service, dialogs, _, _, _) = Create();
        dialogs.SettingsResult = AppSettings.CreateDefault() with { DropDenominator = 123, AlwaysOnTop = true };

        await vm.OpenSettingsCommand.ExecuteAsync(null);

        Assert.Equal(123, service.Current.Settings.DropDenominator);
        Assert.True(vm.AlwaysOnTop);
    }

    [Fact]
    public void TryHandleLocalHotkey_RoutesToCommands()
    {
        var (vm, service, _, _, _, _) = Create();
        service.Initialize(new AppState
        {
            ActiveMatch = new ActiveMatch(Guid.NewGuid(), DateTimeOffset.Now, 0),
        });

        var handled = vm.TryHandleLocalHotkey("OemPlus", HotkeyModifiers.None); // + por defecto

        Assert.True(handled);
        Assert.Equal(1, service.Current.ActiveMatch!.Attempts);
    }

    [Fact]
    public void TryHandleLocalHotkey_UnknownKey_ReturnsFalse()
    {
        var (vm, _, _, _, _, _) = Create();

        Assert.False(vm.TryHandleLocalHotkey("F24", HotkeyModifiers.Control | HotkeyModifiers.Alt));
    }

    [Fact]
    public void GlobalHotkey_RoutesToIncrementAndDecrement()
    {
        var (vm, service, _, hotkeys, _, _) = Create();
        service.Initialize(new AppState
        {
            ActiveMatch = new ActiveMatch(Guid.NewGuid(), DateTimeOffset.Now, 0),
        });

        hotkeys.Raise(App.Abstractions.GlobalHotkeyAction.Increment);
        Assert.Equal(1, service.Current.ActiveMatch!.Attempts);

        hotkeys.Raise(App.Abstractions.GlobalHotkeyAction.Decrement);
        Assert.Equal(0, service.Current.ActiveMatch!.Attempts);
    }

    [Fact]
    public void ApplyLoadResult_AppliesStoredTheme()
    {
        var (vm, _, _, _, themes, _) = Create();
        var state = new AppState
        {
            Settings = AppSettings.CreateDefault() with { Theme = AppTheme.Light },
        };

        vm.ApplyLoadResult(AppStateLoadResult.Loaded(state));

        Assert.Equal(AppTheme.Light, themes.Current);
    }

    [Fact]
    public void RefreshFromState_ReappliesThemeAfterSettingsChange()
    {
        var (vm, service, _, _, themes, _) = Create();
        vm.ApplyLoadResult(AppStateLoadResult.Loaded(AppState.CreateDefault()));
        Assert.Equal(AppTheme.Dark, themes.Current);

        service.Initialize(new AppState
        {
            Settings = AppSettings.CreateDefault() with { Theme = AppTheme.Light },
        });

        Assert.Equal(AppTheme.Light, themes.Current);
    }

    [Fact]
    public void RefreshFromState_UpdatesAlwaysOnTopAndHotkeys()
    {
        var (vm, service, _, hotkeys, _, _) = Create();
        service.Initialize(new AppState
        {
            Settings = AppSettings.CreateDefault() with { AlwaysOnTop = true, EnableGlobalHotkeys = true },
        });

        Assert.True(vm.AlwaysOnTop);
        Assert.True(hotkeys.LastSettings!.EnableGlobalHotkeys);
    }

    [Fact]
    public void AlwaysOnTop_Change_RaisesNotification()
    {
        var (vm, service, _, _, _, _) = Create();
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.AlwaysOnTop))
                raised = true;
        };

        service.Initialize(new AppState
        {
            Settings = AppSettings.CreateDefault() with { AlwaysOnTop = true },
        });

        Assert.True(raised);
    }
}
