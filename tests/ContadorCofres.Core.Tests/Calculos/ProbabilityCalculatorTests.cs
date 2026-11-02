using ContadorCofres.Core.Services;

namespace ContadorCofres.Core.Tests.Calculos;

public class ProbabilityCalculatorTests
{
    // ---- Referencias con p = 0.5 (denominador 2) ----

    [Theory]
    [InlineData(1, 0.5)]
    [InlineData(2, 0.75)]
    [InlineData(3, 0.875)]
    [InlineData(4, 0.9375)]
    [InlineData(10, 0.9990234375)]
    public void AtLeastOneSuccess_CoinFlipReferenceValues(long attempts, double expected)
    {
        var result = ProbabilityCalculator.AtLeastOneSuccess(attempts, 2);
        Assert.Equal(expected, result, 12);
    }

    [Fact]
    public void AtLeastOneSuccess_MatchesNaiveFormulaForManyAttempts()
    {
        for (int n = 1; n <= 25; n++)
        {
            var expected = 1.0 - Math.Pow(0.5, n);
            var actual = ProbabilityCalculator.AtLeastOneSuccess(n, 2);
            Assert.True(Math.Abs(actual - expected) < 1e-12, $"n={n}: {actual} vs {expected}");
        }
    }

    // ---- Casos especiales ----

    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 700000)]
    public void AtLeastOneSuccess_ZeroAttemptsIsZero(long attempts, long denominator)
        => Assert.Equal(0.0, ProbabilityCalculator.AtLeastOneSuccess(attempts, denominator));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(long.MaxValue)]
    public void AtLeastOneSuccess_CertainProbabilityIsOne(long attempts)
        => Assert.Equal(1.0, ProbabilityCalculator.AtLeastOneSuccess(attempts, 1));

    [Fact]
    public void AtLeastOneSuccess_ZeroAttemptsWithCertainProbabilityIsZero()
        => Assert.Equal(0.0, ProbabilityCalculator.AtLeastOneSuccess(0, 1));

    // ---- Validaciones ----

    [Theory]
    [InlineData(-1)]
    [InlineData(-700000)]
    public void AtLeastOneSuccess_NegativeAttemptsThrows(long attempts)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityCalculator.AtLeastOneSuccess(attempts, 2));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AtLeastOneSuccess_InvalidDenominatorThrows(long denominator)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityCalculator.AtLeastOneSuccess(1, denominator));

    // ---- Monotonía y estabilidad ----

    [Fact]
    public void AtLeastOneSuccess_IsStrictlyIncreasingWithAttempts()
    {
        double previous = 0;
        for (long n = 1; n <= 200; n++)
        {
            var current = ProbabilityCalculator.AtLeastOneSuccess(n, 700_000);
            Assert.True(current > previous, $"La probabilidad no creció en n={n}");
            previous = current;
        }
    }

    [Fact]
    public void AtLeastOneSuccess_IsStrictlyIncreasingBeforeSaturation()
    {
        // En este rango ningún valor se satura a 1.0 exacto en doble precisión.
        double previous = 0;
        foreach (long n in new long[] { 1_000, 10_000, 100_000, 700_000, 2_000_000 })
        {
            var current = ProbabilityCalculator.AtLeastOneSuccess(n, 700_000);
            Assert.True(current > previous, $"La probabilidad no creció en n={n}");
            previous = current;
        }

        Assert.True(previous < 1.0, $"Con 2M intentos P debería ser < 1 ({previous})");
    }

    [Fact]
    public void AtLeastOneSuccess_SaturatesToOneForAstronomicalCounts()
    {
        var p1 = ProbabilityCalculator.AtLeastOneSuccess(1_000_000_000L, 700_000);
        var p2 = ProbabilityCalculator.AtLeastOneSuccess(long.MaxValue / 2, 700_000);

        Assert.InRange(p1, 0, 1);
        Assert.Equal(1.0, p2);
        Assert.True(p1 <= p2, "La probabilidad no debe decrecer con los intentos.");
    }

    [Fact]
    public void AtLeastOneSuccess_TinyProbabilityIsNotZero()
    {
        // p = 1e-18, n = 1e9 -> P ~ 1e-9. La fórmula ingenua daría 0 por redondeo.
        const long denominator = 999_999_999_999_999_999;
        const long attempts = 1_000_000_000;

        var result = ProbabilityCalculator.AtLeastOneSuccess(attempts, denominator);

        Assert.True(result > 9.9e-10, $"P debería ser ~1e-9 pero fue {result}");
        Assert.True(result < 1.01e-9, $"P debería ser ~1e-9 pero fue {result}");
    }

    // ---- Intentos para una probabilidad objetivo ----

    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(0.75, 2)]
    [InlineData(0.9, 4)]
    [InlineData(0.95, 5)]
    [InlineData(0.99, 7)]
    public void AttemptsForTarget_CoinFlipReferenceValues(double target, long expected)
        => Assert.Equal(expected, ProbabilityCalculator.AttemptsForTarget(target, 2));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.5)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    public void AttemptsForTarget_InvalidTargetThrows(double target)
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityCalculator.AttemptsForTarget(target, 2));

    [Fact]
    public void AttemptsForTarget_NaNThrows()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityCalculator.AttemptsForTarget(double.NaN, 2));

    [Fact]
    public void AttemptsForTarget_InvalidDenominatorThrows()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityCalculator.AttemptsForTarget(0.5, 0));

    [Fact]
    public void AttemptsForTarget_CertainProbabilityNeedsOneAttempt()
        => Assert.Equal(1, ProbabilityCalculator.AttemptsForTarget(0.99, 1));

    [Fact]
    public void AttemptsForTarget_RoundsUp()
    {
        // Con p = 1/1.000.000, para el 50 % se necesitan 693.147 intentos:
        // con 693.146 todavía no se llega.
        const long denominator = 1_000_000;
        var attempts = ProbabilityCalculator.AttemptsForTarget(0.5, denominator);

        Assert.Equal(693_147, attempts);
        var below = ProbabilityCalculator.AtLeastOneSuccess(attempts!.Value - 1, denominator);
        var exact = ProbabilityCalculator.AtLeastOneSuccess(attempts.Value, denominator);
        Assert.True(below < 0.5, $"{below} debería ser < 0.5");
        Assert.True(exact >= 0.5, $"{exact} debería ser >= 0.5");
    }

    [Fact]
    public void AttemptsForTarget_WithinRangeForDefaultDenominator()
    {
        var attempts = ProbabilityCalculator.AttemptsForTarget(0.99, 700_000);
        Assert.NotNull(attempts);
        Assert.True(attempts > 3_000_000 && attempts < 4_000_000, $"693147*log(0.01)/... = {attempts}");
    }

    [Fact]
    public void AttemptsForTarget_OutOfRangeReturnsNull()
    {
        // Con denominador long.MaxValue, los intentos para el 99 % exceden long.
        var attempts = ProbabilityCalculator.AttemptsForTarget(0.99, long.MaxValue);
        Assert.Null(attempts);
    }

    [Fact]
    public void AttemptsForTarget_OutOfRangeForVeryHighTarget()
    {
        var attempts = ProbabilityCalculator.AttemptsForTarget(0.9999999999, long.MaxValue);
        Assert.Null(attempts);
    }
}
