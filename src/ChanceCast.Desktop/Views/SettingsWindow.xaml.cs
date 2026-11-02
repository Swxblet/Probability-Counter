using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Views;

/// <summary>
/// Code-behind solo para captura de atajos y cierre: ninguna regla de negocio.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
    }

    private SettingsViewModel ViewModel => (SettingsViewModel)DataContext;

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.TrySave())
            DialogResult = true;
        // Si falla, el error queda visible en ValidationError y la ventana sigue abierta.
    }

    private void ClearHotkey_Click(object sender, RoutedEventArgs e)
    {
        SetHotkey((string)((Button)sender).Tag, null);
    }

    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Tab sigue navegando; Escape conserva el valor anterior.
        if (e.Key == Key.Tab)
            return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            return;
        }

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (IsModifierKey(key) || key == Key.None)
            return;

        SetHotkey((string)((TextBox)sender).Tag, new HotkeyBinding(key.ToString(), ToCoreModifiers(Keyboard.Modifiers)));
        e.Handled = true;
    }

    private void SetHotkey(string tag, HotkeyBinding? binding)
    {
        switch (tag)
        {
            case "Increment": ViewModel.IncrementHotkey = binding; break;
            case "Decrement": ViewModel.DecrementHotkey = binding; break;
            case "Edit": ViewModel.EditCountHotkey = binding; break;
            case "Start": ViewModel.StartMatchHotkey = binding; break;
        }
    }

    private static bool IsModifierKey(Key key) => key is
        Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
        Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or
        Key.CapsLock or Key.NumLock or Key.Scroll or Key.Apps;

    private static HotkeyModifiers ToCoreModifiers(ModifierKeys modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= HotkeyModifiers.Windows;
        return result;
    }
}
