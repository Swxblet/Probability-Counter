using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ChanceCast.App.Abstractions;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Infrastructure;

/// <summary>
/// Atajos globales mediante RegisterHotKey de Windows. Solo reciben el aviso
/// "se pulsó X" del sistema operativo: no leen ni modifican el juego.
/// Solo se registran +1/-1 (Editar/Nueva quedan como atajos locales).
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int WmHotKey = 0x0312;
    private const int IdIncrement = 1;
    private const int IdDecrement = 2;

    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private HwndSource? _source;
    private Window? _window;
    private IntPtr _handle;
    private bool _disposed;

    public event EventHandler<GlobalHotkeyAction>? Pressed;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Asocia el servicio a la ventana principal para recibir WM_HOTKEY.</summary>
    public void Attach(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        if (PresentationSource.FromVisual(window) is HwndSource ready)
        {
            Hook(ready);
        }
        else
        {
            window.SourceInitialized += OnSourceInitialized;
        }

        window.Closed += (_, _) => Dispose();
    }

    public IReadOnlyList<string> Update(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (_disposed)
            return [];

        UnregisterAll();

        if (!settings.EnableGlobalHotkeys || _handle == IntPtr.Zero)
            return [];

        var errors = new List<string>(2);
        Register(settings.IncrementHotkey, IdIncrement, "sumar (+1)", errors);
        Register(settings.DecrementHotkey, IdDecrement, "restar (−1)", errors);
        return errors;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        UnregisterAll();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _handle = IntPtr.Zero;
        if (_window is not null)
        {
            _window.SourceInitialized -= OnSourceInitialized;
            _window = null;
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (HwndSource.FromHwnd(((Window)sender!).GetHandle()) is { } source)
            Hook(source);
    }

    private void Hook(HwndSource source)
    {
        _source = source;
        _handle = source.Handle;
        _source.AddHook(WndProc);
    }

    private void Register(HotkeyBinding? binding, int id, string actionName, List<string> errors)
    {
        if (binding is null)
            return;

        if (!Enum.TryParse<Key>(binding.Key, out var key))
        {
            errors.Add($"El atajo global de {actionName} ({binding.DisplayName}) no es una tecla válida en este sistema.");
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            errors.Add($"El atajo global de {actionName} ({binding.DisplayName}) no se puede registrar en este teclado.");
            return;
        }

        if (!RegisterHotKey(_handle, id, ToNative(binding.Modifiers), (uint)virtualKey))
            errors.Add($"No se pudo registrar el atajo global de {actionName} ({binding.DisplayName}): está en uso por otra aplicación.");
    }

    private void UnregisterAll()
    {
        if (_handle == IntPtr.Zero)
            return;
        UnregisterHotKey(_handle, IdIncrement);
        UnregisterHotKey(_handle, IdDecrement);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotKey)
        {
            var id = wParam.ToInt32();
            if (id == IdIncrement)
            {
                Pressed?.Invoke(this, GlobalHotkeyAction.Increment);
                handled = true;
            }
            else if (id == IdDecrement)
            {
                Pressed?.Invoke(this, GlobalHotkeyAction.Decrement);
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    private static uint ToNative(HotkeyModifiers modifiers)
    {
        uint native = 0;
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) native |= ModAlt;
        if (modifiers.HasFlag(HotkeyModifiers.Control)) native |= ModControl;
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) native |= ModShift;
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) native |= ModWin;
        return native;
    }
}

/// <summary>Obtiene el HWND de una ventana WPF.</summary>
internal static class WindowHandleExtensions
{
    internal static IntPtr GetHandle(this Window window)
        => new WindowInteropHelper(window).Handle;
}
