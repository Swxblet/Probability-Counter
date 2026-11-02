using System.Globalization;

namespace ChanceCast.Core.Services;

/// <summary>
/// Formato de presentación de probabilidades y valores derivados.
/// Mantiene precisión interna; el redondeo es solo para mostrar.
/// </summary>
public static class ProbabilityFormatter
{
    /// <summary>
    /// Formatea una probabilidad como porcentaje. Las probabilidades muy
    /// pequeñas no se muestran como cero exacto y las cercanas a 1 no se
    /// muestran como garantía.
    /// </summary>
    public static string FormatPercent(double probability, CultureInfo? culture = null)
    {
        if (double.IsNaN(probability))
            throw new ArgumentOutOfRangeException(nameof(probability), "La probabilidad no puede ser NaN.");

        culture ??= CultureInfo.CurrentCulture;
        probability = Math.Clamp(probability, 0.0, 1.0);

        if (probability <= 0.0) return "0 %";
        if (probability >= 1.0) return "100 %";

        double percent = probability * 100.0;
        if (percent < 0.0001)
            return "< " + 0.0001.ToString("0.####", culture) + " %";
        if (percent > 99.9999)
            return "> " + 99.9999.ToString("0.####", culture) + " %";

        return percent.ToString("0.####", culture) + " %";
    }

    public static string FormatLong(long value, CultureInfo? culture = null)
        => value.ToString("N0", culture ?? CultureInfo.CurrentCulture);

    /// <summary>Formatea un valor que puede exceder el rango de long ("Fuera de rango").</summary>
    public static string FormatLong(long? value, CultureInfo? culture = null)
        => value is null ? "Fuera de rango" : FormatLong(value.Value, culture);

    /// <summary>Formatea el promedio observado; null significa "Sin datos".</summary>
    public static string FormatAverage(double? average, CultureInfo? culture = null)
        => average is null ? "Sin datos" : average.Value.ToString("0.##", culture ?? CultureInfo.CurrentCulture);

    /// <summary>Formatea la proyección de sesiones para un objetivo.</summary>
    public static string FormatProjection(MatchProjection projection, CultureInfo? culture = null)
        => projection.Kind switch
        {
            ProjectionKind.Value => FormatLong(projection.Matches ?? 0, culture),
            ProjectionKind.NoData => "Sin datos",
            ProjectionKind.NotEstimable => "No estimable",
            ProjectionKind.OutOfRange => "Fuera de rango",
            _ => "—",
        };
}
