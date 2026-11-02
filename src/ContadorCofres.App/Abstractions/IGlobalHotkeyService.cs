using ContadorCofres.Core.Models;

namespace ContadorCofres.App.Abstractions;

/// <summary>Acción invocable desde un atajo global del sistema operativo.</summary>
public enum GlobalHotkeyAction
{
    Increment,
    Decrement,
}

/// <summary>
/// Atajos globales (RegisterHotKey de Windows). Solo reciben "se pulsó X" del
/// sistema operativo: no leen ni modifican el juego. La implementación vive en
/// el proyecto Desktop; en pruebas se usa un doble.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    event EventHandler<GlobalHotkeyAction>? Pressed;

    /// <summary>
    /// Registra +1/-1 según la configuración. Devuelve errores no fatales
    /// (p. ej. atajo en uso por otra aplicación).
    /// </summary>
    IReadOnlyList<string> Update(AppSettings settings);
}
