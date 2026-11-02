using ChanceCast.App.Abstractions;
using ChanceCast.App.Services;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.Desktop.Tests;

/// <summary>Almacén en memoria para pruebas de ViewModels.</summary>
public sealed class FakeStore : IAppStateStore
{
    public AppState State { get; set; } = AppState.CreateDefault();
    public bool FailNextSave { get; set; }

    public AppStateLoadResult Load() => AppStateLoadResult.Loaded(State);

    public Task<SaveResult> SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        if (FailNextSave)
        {
            FailNextSave = false;
            return Task.FromResult(SaveResult.Fail("fallo simulado al guardar"));
        }

        State = state;
        return Task.FromResult(SaveResult.Ok());
    }
}

/// <summary>Diálogos controlables para pruebas de ViewModels.</summary>
public sealed class FakeDialogs : IDialogService
{
    public bool ConfirmResult { get; set; } = true;
    public int ConfirmCalls { get; private set; }
    public long? PromptResult { get; set; } = 10;
    public string? SavePathResult { get; set; } = null;
    public bool SummaryResult { get; set; }
    public AppSettings? SettingsResult { get; set; } = null;
    public List<string> Errors { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string confirmButtonText = "Aceptar")
    {
        ConfirmCalls++;
        return Task.FromResult(ConfirmResult);
    }

    public void ShowError(string message) => Errors.Add(message);

    public void ShowInfo(string title, string message)
    {
    }

    public Task<long?> PromptAttemptsAsync(string title, string label, long current)
        => Task.FromResult(PromptResult);

    public Task<string?> GetCsvSavePathAsync(string suggestedFileName)
        => Task.FromResult(SavePathResult);

    public Task<bool> ShowSummaryAsync(MatchSummaryData data) => Task.FromResult(SummaryResult);

    public Task<AppSettings?> ShowSettingsAsync(AppSettings current) => Task.FromResult(SettingsResult);
}

/// <summary>Atajos globales simulados para pruebas de ViewModels.</summary>
public sealed class FakeHotkeys : IGlobalHotkeyService
{
    public event EventHandler<GlobalHotkeyAction>? Pressed;

    public AppSettings? LastSettings { get; private set; }

    public void Dispose()
    {
    }

    public IReadOnlyList<string> Update(AppSettings settings)
    {
        LastSettings = settings;
        return [];
    }

    public void Raise(GlobalHotkeyAction action) => Pressed?.Invoke(this, action);
}

/// <summary>Temas simulados para pruebas de ViewModels.</summary>
public sealed class FakeThemeManager : App.Abstractions.IThemeManager
{
    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public List<AppTheme> Applied { get; } = [];

    public void Apply(AppTheme theme)
    {
        Current = theme;
        Applied.Add(theme);
    }
}

public static class ViewModelFactory
{
    public static (MatchService Service, FakeStore Store, FakeDialogs Dialogs, FakeHotkeys Hotkeys, AppStatus Status) Create(
        AppState? initial = null,
        TimeProvider? clock = null)
    {
        var store = new FakeStore();
        var service = new MatchService(store, NullLogger.Instance, clock);
        service.Initialize(initial ?? AppState.CreateDefault());
        return (service, store, new FakeDialogs(), new FakeHotkeys(), new AppStatus());
    }
}
