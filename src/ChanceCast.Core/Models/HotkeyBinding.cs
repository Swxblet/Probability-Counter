namespace ChanceCast.Core.Models;

/// <summary>
/// Modificadores de teclado admitidos para los atajos configurables.
/// </summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

/// <summary>
/// Combinación de teclas configurable.
/// <see cref="Key"/> guarda el nombre normalizado de la tecla principal
/// (por ejemplo "F5", "OemPlus" o "N"), equivalente al nombre del enum
/// System.Windows.Input.Key de WPF, pero sin depender de WPF.
/// </summary>
public sealed record HotkeyBinding(string Key, HotkeyModifiers Modifiers)
{
    /// <summary>Nombre legible de la combinación, por ejemplo "Ctrl + N".</summary>
    public string DisplayName
    {
        get
        {
            var parts = new List<string>(5);
            if (Modifiers.HasFlag(HotkeyModifiers.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(HotkeyModifiers.Windows)) parts.Add("Win");
            parts.Add(Key);
            return string.Join(" + ", parts);
        }
    }
}
