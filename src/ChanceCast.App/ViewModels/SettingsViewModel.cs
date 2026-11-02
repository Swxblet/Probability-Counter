using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.App.ViewModels;

/// <summary>
/// Edición de la configuración sobre una copia. Solo al guardar con éxito se
/// devuelve el resultado; cancelar no modifica nada.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const string ProbabilityChangeWarning =
        "Los cálculos se actualizarán usando esta probabilidad para todos los intentos incluidos. " +
        "Si corresponden a otro ítem o a una tasa diferente, el historial podría no ser comparable.";

    [ObservableProperty]
    private string _eventName = "Evento objetivo";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _denominatorText = AppSettings.DefaultDropDenominator.ToString();

    /// <summary>Equivalente en porcentaje del denominador, solo lectura.</summary>
    [ObservableProperty]
    private string _denominatorPercentText = "—";

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private bool _confirmFinish = true;

    [ObservableProperty]
    private bool _enableGlobalHotkeys;

    /// <summary>True = oscuro, false = claro.</summary>
    [ObservableProperty]
    private bool _isDarkTheme = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private HotkeyBinding? _incrementHotkey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private HotkeyBinding? _decrementHotkey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private HotkeyBinding? _editCountHotkey;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private HotkeyBinding? _startMatchHotkey;

    [ObservableProperty]
    private string _incrementDisplay = string.Empty;

    [ObservableProperty]
    private string _decrementDisplay = string.Empty;

    [ObservableProperty]
    private string _editCountDisplay = string.Empty;

    [ObservableProperty]
    private string _startMatchDisplay = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    public SettingsViewModel(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EventName = settings.EventName;
        DenominatorText = settings.DropDenominator.ToString();
        AlwaysOnTop = settings.AlwaysOnTop;
        ConfirmFinish = settings.ConfirmFinish;
        EnableGlobalHotkeys = settings.EnableGlobalHotkeys;
        IsDarkTheme = settings.Theme == AppTheme.Dark;
        IncrementHotkey = settings.IncrementHotkey;
        DecrementHotkey = settings.DecrementHotkey;
        EditCountHotkey = settings.EditCountHotkey;
        StartMatchHotkey = settings.StartMatchHotkey;
        // El valor inicial puede coincidir con el del campo y no notificar cambio.
        DenominatorPercentText = PercentOf(DenominatorText);
    }

    /// <summary>Resultado validado tras <see cref="TrySave"/>; null si no se guardó.</summary>
    public AppSettings? Result { get; private set; }

    partial void OnIncrementHotkeyChanged(HotkeyBinding? value) => IncrementDisplay = DisplayOf(value);
    partial void OnDenominatorTextChanged(string value) => DenominatorPercentText = PercentOf(value);
    partial void OnDecrementHotkeyChanged(HotkeyBinding? value) => DecrementDisplay = DisplayOf(value);
    partial void OnEditCountHotkeyChanged(HotkeyBinding? value) => EditCountDisplay = DisplayOf(value);
    partial void OnStartMatchHotkeyChanged(HotkeyBinding? value) => StartMatchHotkeyChanged(value);

    private void StartMatchHotkeyChanged(HotkeyBinding? value) => StartMatchDisplay = DisplayOf(value);

    private static string DisplayOf(HotkeyBinding? binding) => binding?.DisplayName ?? "Sin asignar";

    private static string PercentOf(string text)
    {
        if (long.TryParse(text.Trim(), out var denominator) && denominator >= 1)
            return "≈ " + ProbabilityFormatter.FormatPercent(1.0 / denominator, CultureInfo.InvariantCulture);
        return "—";
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(DenominatorText);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => TrySave();

    /// <summary>
    /// Valida y construye el resultado. Devuelve false y expone
    /// <see cref="ValidationError"/> sin cerrar el diálogo.
    /// </summary>
    public bool TrySave()
    {
        if (!long.TryParse(DenominatorText.Trim(), out var denominator) || denominator < 1)
        {
            ValidationError = "El denominador debe ser un número entero mayor o igual a 1.";
            return false;
        }

        var hotkeys = new (string Name, HotkeyBinding? Binding)[]
        {
            ("Sumar", IncrementHotkey),
            ("Restar", DecrementHotkey),
            ("Editar total", EditCountHotkey),
            ("Nueva sesión", StartMatchHotkey),
        };

        var duplicates = hotkeys
            .Where(h => h.Binding is not null)
            .GroupBy(h => h.Binding!)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" y ", g.Select(h => h.Name)))
            .ToList();

        if (duplicates.Count > 0)
        {
            ValidationError = "Dos acciones usan el mismo atajo (" + string.Join("; ", duplicates) + "). Asigná combinaciones distintas o dejá alguna sin asignar.";
            return false;
        }

        foreach (var (_, binding) in hotkeys)
        {
            if (binding is not null && string.IsNullOrWhiteSpace(binding.Key))
            {
                ValidationError = "Un atajo configurado tiene la tecla vacía.";
                return false;
            }
        }

        ValidationError = null;
        Result = new AppSettings
        {
            EventName = EventName,
            DropDenominator = denominator,
            AlwaysOnTop = AlwaysOnTop,
            ConfirmFinish = ConfirmFinish,
            EnableGlobalHotkeys = EnableGlobalHotkeys,
            IncrementHotkey = IncrementHotkey,
            DecrementHotkey = DecrementHotkey,
            EditCountHotkey = EditCountHotkey,
            StartMatchHotkey = StartMatchHotkey,
            Theme = IsDarkTheme ? AppTheme.Dark : AppTheme.Light,
        };
        return true;
    }
}
