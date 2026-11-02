namespace ChanceCast.Core.Models;

/// <summary>
/// Configuración de la aplicación. El denominador es un dato proporcionado
/// por el usuario; la aplicación no lo verifica contra el juego.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Probabilidad predeterminada: 1 entre 700.000 por intento.</summary>
    public const long DefaultDropDenominator = 700_000;

    public string EventName { get; init; } = "Evento objetivo";
    public long DropDenominator { get; init; } = DefaultDropDenominator;
    public bool AlwaysOnTop { get; init; }
    public bool ConfirmFinish { get; init; } = true;

    /// <summary>
    /// Activa el registro de los atajos de suma y resta como teclas globales,
    /// para contar aunque otra aplicación esté enfocada. Desactivado por defecto.
    /// </summary>
    public bool EnableGlobalHotkeys { get; init; }

    public HotkeyBinding? IncrementHotkey { get; init; } = new("OemPlus", HotkeyModifiers.None);
    public HotkeyBinding? DecrementHotkey { get; init; } = new("OemMinus", HotkeyModifiers.None);
    public HotkeyBinding? EditCountHotkey { get; init; } = new("E", HotkeyModifiers.Control);
    public HotkeyBinding? StartMatchHotkey { get; init; } = new("N", HotkeyModifiers.Control);

    /// <summary>Tema visual. Oscuro por defecto.</summary>
    public AppTheme Theme { get; init; } = AppTheme.Dark;

    public static AppSettings CreateDefault() => new();
}
