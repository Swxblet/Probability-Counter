using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ContadorCofres.Desktop.Views;

/// <summary>Colapsa el elemento cuando el valor enlazado es null o vacío.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Muestra el estado de la partida en el contador.</summary>
public sealed class BoolToMatchStateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "PARTIDA EN CURSO" : "SIN PARTIDA ACTIVA";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
