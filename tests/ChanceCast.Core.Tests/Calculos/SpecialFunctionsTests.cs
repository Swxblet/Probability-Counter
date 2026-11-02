using ChanceCast.Core.Services;

namespace ChanceCast.Core.Tests.Calculos;

public class SpecialFunctionsTests
{
    [Theory]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(10.0)]
    [InlineData(-0.5)]
    [InlineData(-0.9)]
    public void Log1p_MatchesDirectFormulaForModerateValues(double x)
    {
        var expected = Math.Log(1.0 + x);
        var actual = SpecialFunctions.Log1p(x);

        Assert.True(
            Math.Abs(actual - expected) <= 1e-12 * Math.Abs(expected) + 1e-15,
            $"Log1p({x}) = {actual}, esperaba {expected}");
    }

    [Fact]
    public void Log1p_TinyValueAvoidsCancellation()
    {
        // Math.Log(1 + 1e-16) devuelve 0; log1p debe devolver ~1e-16.
        var result = SpecialFunctions.Log1p(1e-16);
        Assert.True(result > 0, "Log1p(1e-16) no debería ser cero.");
        Assert.True(Math.Abs(result - 1e-16) < 1e-30, $"Log1p(1e-16) = {result}");
    }

    [Theory]
    [InlineData(1e-8)]
    [InlineData(-1e-8)]
    [InlineData(1e-12)]
    [InlineData(-1e-12)]
    public void Log1p_TinyValuesAreAccurate(double x)
    {
        // Referencia de alta precisión: serie de Taylor log(1+x) = x - x²/2 + x³/3 - …
        double expected = x;
        double term = x;
        for (int k = 2; k <= 8; k++)
        {
            term *= -x;
            expected += term / k;
        }

        var actual = SpecialFunctions.Log1p(x);
        Assert.True(Math.Abs(actual - expected) <= Math.Abs(expected) * 1e-14 + 1e-30,
            $"Log1p({x}) = {actual}, esperaba {expected}");
    }

    [Fact]
    public void Log1p_ZeroIsZero() => Assert.Equal(0.0, SpecialFunctions.Log1p(0.0));

    [Fact]
    public void Log1p_MinusOneIsMinusInfinity() => Assert.Equal(double.NegativeInfinity, SpecialFunctions.Log1p(-1.0));

    [Fact]
    public void Log1p_BelowMinusOneIsNaN() => Assert.True(double.IsNaN(SpecialFunctions.Log1p(-1.5)));

    [Fact]
    public void Log1p_NaNPropagates() => Assert.True(double.IsNaN(SpecialFunctions.Log1p(double.NaN)));

    [Fact]
    public void Log1p_NearMinusOneIsFiniteAndNegative()
    {
        var result = SpecialFunctions.Log1p(-0.999999);
        Assert.True(result < -13.8 && result > -13.9, $"Log1p(-0.999999) = {result}");
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(-0.5)]
    [InlineData(0.6931471805599453)]
    [InlineData(1.0)]
    [InlineData(-2.0)]
    [InlineData(5.0)]
    public void Expm1_MatchesDirectFormulaForModerateValues(double x)
    {
        var expected = Math.Exp(x) - 1.0;
        var actual = SpecialFunctions.Expm1(x);

        Assert.True(
            Math.Abs(actual - expected) <= 1e-12 * Math.Abs(expected) + 1e-15,
            $"Expm1({x}) = {actual}, esperaba {expected}");
    }

    [Fact]
    public void Expm1_TinyValueAvoidsCancellation()
    {
        // Math.Exp(1e-16) - 1 devuelve 0; expm1 debe devolver ~1e-16.
        var result = SpecialFunctions.Expm1(1e-16);
        Assert.True(result > 0, "Expm1(1e-16) no debería ser cero.");
        Assert.True(Math.Abs(result - 1e-16) < 1e-30, $"Expm1(1e-16) = {result}");
    }

    [Fact]
    public void Expm1_ZeroIsZero() => Assert.Equal(0.0, SpecialFunctions.Expm1(0.0));

    [Fact]
    public void Expm1_VeryNegativeTendsToMinusOne() => Assert.Equal(-1.0, SpecialFunctions.Expm1(-1000.0));

    [Fact]
    public void Expm1_VeryPositiveOverflowsToInfinity() => Assert.Equal(double.PositiveInfinity, SpecialFunctions.Expm1(1000.0));

    [Fact]
    public void Expm1_NaNPropagates() => Assert.True(double.IsNaN(SpecialFunctions.Expm1(double.NaN)));
}
