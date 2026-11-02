using ChanceCast.Core.Models;

namespace ChanceCast.App.Abstractions;

/// <summary>
/// Aplica el tema visual. La implementación WPF intercambia el diccionario de
/// recursos; en pruebas se usa un doble que solo registra.
/// </summary>
public interface IThemeManager
{
    AppTheme Current { get; }

    void Apply(AppTheme theme);
}
