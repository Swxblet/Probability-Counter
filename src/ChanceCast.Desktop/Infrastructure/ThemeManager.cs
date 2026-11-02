using System.Windows;
using ChanceCast.App.Abstractions;
using ChanceCast.Core.Models;

namespace ChanceCast.Desktop.Infrastructure;

/// <summary>
/// Intercambia el diccionario de tema (claro/oscuro) en tiempo de ejecución.
/// No necesita HWND: puede aplicarse antes de mostrar la ventana.
/// </summary>
public sealed class ThemeManager : IThemeManager
{
    private readonly Application _app;

    public ThemeManager(Application app)
    {
        _app = app ?? throw new ArgumentNullException(nameof(app));
    }

    public AppTheme Current { get; private set; } = AppTheme.Dark;

    public void Apply(AppTheme theme)
    {
        Current = theme;
        var source = new Uri($"Resources/Themes/{theme}.xaml", UriKind.Relative);
        var merged = _app.Resources.MergedDictionaries;

        for (var i = 0; i < merged.Count; i++)
        {
            var existing = merged[i].Source?.OriginalString;
            if (existing is not null && existing.Contains("/Themes/", StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = new ResourceDictionary { Source = source };
                return;
            }
        }

        merged.Insert(0, new ResourceDictionary { Source = source });
    }
}
