using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ChanceCast.App.ViewModels;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Views;

/// <summary>
/// Solo comportamiento visual: enrutado de atajos locales. Nunca se consumen
/// atajos mientras el foco está en un TextBox para no interferir al escribir.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsEditingText(e.OriginalSource))
            return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape or Key.None)
            return;

        if (ViewModel.TryHandleLocalHotkey(key.ToString(), ToCoreModifiers(Keyboard.Modifiers)))
            e.Handled = true;
    }

    private static bool IsEditingText(object source)
    {
        DependencyObject? current = source as DependencyObject;
        while (current is not null)
        {
            if (current is TextBox)
                return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

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
