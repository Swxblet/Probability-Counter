using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChanceCast.App.Abstractions;
using ChanceCast.App.Services;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.App.ViewModels;

/// <summary>
/// Coordinación general: expone los ViewModels de cada pestaña, el estado
/// visible, la configuración y el enrutado de atajos locales y globales.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly MatchService _service;
    private readonly IDialogService _dialogs;
    private readonly IGlobalHotkeyService _hotkeys;

    [ObservableProperty]
    private bool _alwaysOnTop;

    public MainViewModel(
        MatchService service,
        IDialogService dialogs,
        IGlobalHotkeyService hotkeys,
        AppStatus status,
        CounterViewModel counter,
        HistoryViewModel history,
        StatisticsViewModel statistics)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _hotkeys = hotkeys ?? throw new ArgumentNullException(nameof(hotkeys));
        Status = status ?? throw new ArgumentNullException(nameof(status));
        Counter = counter ?? throw new ArgumentNullException(nameof(counter));
        History = history ?? throw new ArgumentNullException(nameof(history));
        Statistics = statistics ?? throw new ArgumentNullException(nameof(statistics));

        _service.StateChanged += (_, _) => RefreshFromState();
        _hotkeys.Pressed += (_, action) => OnGlobalHotkey(action);
        RefreshFromState();
    }

    public AppStatus Status { get; }

    public CounterViewModel Counter { get; }

    public HistoryViewModel History { get; }

    public StatisticsViewModel Statistics { get; }

    public string AppVersion { get; } = "ChanceCast 1.0.0";

    /// <summary>
    /// Aplica el resultado de la carga inicial: estado vacío, datos cargados,
    /// respaldo restaurado o esquema incompatible (sin escritura).
    /// </summary>
    public void ApplyLoadResult(AppStateLoadResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.State is not null)
            _service.Initialize(result.State);
        else
            _service.Initialize(AppState.CreateDefault());

        RefreshFromState();

        switch (result.Status)
        {
            case LoadStatus.Loaded:
                Status.SetOk();
                break;
            case LoadStatus.RecoveredFromBackup:
                Status.SetError(result.Message ?? "Se restauró la copia de seguridad.");
                break;
            case LoadStatus.IncompatibleSchema:
                Status.SetError(result.Message ?? "Los datos usan una versión más reciente. No se guardarán cambios.");
                break;
            case LoadStatus.NoData when result.FilesExisted:
                Status.SetError(result.Message ?? "No se pudieron leer los datos. Se inicia vacío sin modificar archivos.");
                break;
            default:
                Status.SetOk("Sin datos previos. Iniciá tu primera sesión.");
                break;
        }
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var edited = await _dialogs.ShowSettingsAsync(_service.Current.Settings);
        if (edited is null)
            return; // cancelar no modifica

        var result = await _service.UpdateSettingsAsync(edited);
        if (!result.Success)
            Status.SetError(result.ErrorMessage!);
    }

    /// <summary>
    /// Enruta un atajo local (ventana enfocada). Devuelve true si se consumió.
    /// La vista no lo invoca cuando el foco está en un TextBox.
    /// </summary>
    public bool TryHandleLocalHotkey(string key, HotkeyModifiers modifiers)
    {
        var settings = _service.Current.Settings;
        var binding = new HotkeyBinding(key, modifiers);

        if (Equals(binding, settings.IncrementHotkey) && Counter.IncrementCommand.CanExecute(null))
        {
            Counter.IncrementCommand.Execute(null);
            return true;
        }

        if (Equals(binding, settings.DecrementHotkey) && Counter.DecrementCommand.CanExecute(null))
        {
            Counter.DecrementCommand.Execute(null);
            return true;
        }

        if (Equals(binding, settings.EditCountHotkey) && Counter.EditCountCommand.CanExecute(null))
        {
            Counter.EditCountCommand.Execute(null);
            return true;
        }

        if (Equals(binding, settings.StartMatchHotkey) && Counter.StartMatchCommand.CanExecute(null))
        {
            Counter.StartMatchCommand.Execute(null);
            return true;
        }

        return false;
    }

    private void OnGlobalHotkey(GlobalHotkeyAction action)
    {
        // Solo +1/-1 tienen registro global; se respetan los CanExecute.
        if (action == GlobalHotkeyAction.Increment && Counter.IncrementCommand.CanExecute(null))
            Counter.IncrementCommand.Execute(null);
        else if (action == GlobalHotkeyAction.Decrement && Counter.DecrementCommand.CanExecute(null))
            Counter.DecrementCommand.Execute(null);
    }

    private void RefreshFromState()
    {
        AlwaysOnTop = _service.Current.Settings.AlwaysOnTop;
        RefreshHotkeys();
    }

    /// <summary>
    /// Registra los atajos globales con el HWND actual. Se llama al mostrar
    /// la ventana (antes no hay handle) y en cada cambio de estado.
    /// </summary>
    public void RefreshHotkeys()
    {
        var warnings = _hotkeys.Update(_service.Current.Settings);
        if (warnings.Count > 0)
            Status.SetError(string.Join(" ", warnings));
    }
}
