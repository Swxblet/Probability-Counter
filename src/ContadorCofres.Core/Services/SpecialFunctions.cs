namespace ContadorCofres.Core.Services;

/// <summary>
/// Funciones matemáticas estables para valores pequeños, equivalentes a
/// log1p y expm1. System.Math no expone estas funciones públicamente, por lo
/// que se implementan con series con validación propia.
/// </summary>
public static class SpecialFunctions
{
    /// <summary>
    /// Calcula log(1 + x) con precisión completa incluso para |x| muy pequeño,
    /// donde Math.Log(1 + x) devolvería 0 por cancelación.
    /// </summary>
    public static double Log1p(double x)
    {
        if (double.IsNaN(x)) return double.NaN;
        if (x == 0) return 0;
        if (x == -1) return double.NegativeInfinity;
        if (x < -1) return double.NaN;

        // Para |x| > 0.375 la fórmula directa pierde poco respecto al resultado.
        if (x > 0.375 || x < -0.375)
            return Math.Log(1.0 + x);

        // log(1+x) = 2 * z * (1 + z²/3 + z⁴/5 + …) con z = x / (2 + x).
        // Para |x| <= 0.375, |z| <= 0.158 y el residuo de la serie es < 1e-21.
        double z = x / (2.0 + x);
        double z2 = z * z;
        double series = 0.0;
        for (int k = 12; k >= 0; k--)
            series = 1.0 / (2 * k + 1) + z2 * series;

        return 2.0 * z * series;
    }

    /// <summary>
    /// Calcula e^x - 1 con precisión completa incluso para |x| muy pequeño,
    /// donde Math.Exp(x) - 1 devolvería 0 por cancelación.
    /// </summary>
    public static double Expm1(double x)
    {
        if (double.IsNaN(x)) return double.NaN;
        if (x == 0) return 0;

        // Math.Exp desborda por encima de ~709.78; por debajo de -745, e^x - 1 = -1.
        if (x > 709.78) return double.PositiveInfinity;
        if (x < -709.78) return -1.0;

        // Para |x| > ln(2) la resta directa pierde poco error relativo.
        if (x > 0.6931471805599453 || x < -0.6931471805599453)
        {
            double exp = Math.Exp(x);
            return exp == double.PositiveInfinity ? double.PositiveInfinity : exp - 1.0;
        }

        // Serie de Taylor con Horner: e^x - 1 = x·(1 + x/2·(1 + x/3·(1 + …))).
        // Para |x| <= ln(2), el residuo tras 17 términos es < 1e-18.
        double series = 1.0;
        for (int k = 17; k >= 2; k--)
            series = 1.0 + x * series / k;

        return x * series;
    }
}
