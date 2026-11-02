namespace ContadorCofres.Core.Services;

/// <summary>
/// Cálculos de probabilidad bajo el supuesto de intentos independientes con
/// probabilidad constante p = 1 / denominador. El denominador es un dato
/// proporcionado por el usuario, no una probabilidad verificada.
/// </summary>
public static class ProbabilityCalculator
{
    /// <summary>
    /// Probabilidad de al menos un éxito en <paramref name="attempts"/> intentos:
    /// P = 1 - (1 - p)^n = -expm1(n * log1p(-p)), forma estable para p pequeño.
    /// </summary>
    public static double AtLeastOneSuccess(long attempts, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempts);
        ArgumentOutOfRangeException.ThrowIfLessThan(denominator, 1);

        if (attempts == 0) return 0.0;
        if (denominator == 1) return 1.0;

        double p = 1.0 / denominator;
        double exponent = attempts * SpecialFunctions.Log1p(-p);
        double result = -SpecialFunctions.Expm1(exponent);

        // Guardas contra errores de redondeo: el resultado exacto está en [0, 1].
        if (double.IsNaN(result))
            throw new InvalidOperationException("El cálculo de probabilidad produjo un valor no válido.");
        if (result < 0.0) result = 0.0;
        if (result > 1.0) result = 1.0;
        return result;
    }

    /// <summary>
    /// Cantidad de intentos necesarios para alcanzar una probabilidad objetivo,
    /// con redondeo hacia arriba: n = ceil(log(1-q) / log(1-p)).
    /// Devuelve null ("Fuera de rango") si el resultado no cabe en un long.
    /// No existe cantidad finita que alcance el 100 % cuando p &lt; 1.
    /// </summary>
    public static long? AttemptsForTarget(double targetProbability, long denominator)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(denominator, 1);
        if (double.IsNaN(targetProbability) || targetProbability <= 0.0 || targetProbability >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(targetProbability), "La probabilidad objetivo debe ser mayor que 0 y menor que 1.");

        // Con p = 1, un solo intento alcanza cualquier objetivo válido.
        if (denominator == 1) return 1;

        double logNoSuccess = SpecialFunctions.Log1p(-1.0 / denominator); // log(1-p) < 0
        double logNoTarget = SpecialFunctions.Log1p(-targetProbability);  // log(1-q) < 0
        double raw = Math.Ceiling(logNoTarget / logNoSuccess);
        if (raw < 1.0) raw = 1.0;

        // Comprobar que el resultado cabe en long antes de convertirlo.
        if (raw >= (double)long.MaxValue)
            return null;

        return (long)raw;
    }
}
