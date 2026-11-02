using System.Globalization;
using ChanceCast.Core.Services;

namespace ChanceCast.Core.Tests.Calculos;

public class ProbabilityFormatterTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Theory]
    [InlineData(0.0, "0 %")]
    [InlineData(1.0, "100 %")]
    [InlineData(0.5, "50 %")]
    [InlineData(0.638123, "63.8123 %")]
    [InlineData(0.875, "87.5 %")]
    [InlineData(0.999999, "99.9999 %")]
    public void FormatPercent_CommonValues(double probability, string expected)
        => Assert.Equal(expected, ProbabilityFormatter.FormatPercent(probability, Invariant));

    [Fact]
    public void FormatPercent_TinyProbabilityIsNotShownAsExactZero()
        => Assert.Equal("< 0.0001 %", ProbabilityFormatter.FormatPercent(1e-9, Invariant));

    [Fact]
    public void FormatPercent_HalfOfTheDisplayThresholdStillFormats()
        => Assert.Equal("< 0.0001 %", ProbabilityFormatter.FormatPercent(5e-7, Invariant));

    [Fact]
    public void FormatPercent_NearOneIsNotShownAsGuarantee()
        => Assert.Equal("> 99.9999 %", ProbabilityFormatter.FormatPercent(0.9999999, Invariant));

    [Theory]
    [InlineData(-0.5, "0 %")]
    [InlineData(1.5, "100 %")]
    public void FormatPercent_ClampsOutOfRangeValues(double probability, string expected)
        => Assert.Equal(expected, ProbabilityFormatter.FormatPercent(probability, Invariant));

    [Fact]
    public void FormatPercent_NaNThrows()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ProbabilityFormatter.FormatPercent(double.NaN, Invariant));

    [Fact]
    public void FormatLong_NullIsOutOfRange()
        => Assert.Equal("Fuera de rango", ProbabilityFormatter.FormatLong((long?)null, Invariant));

    [Fact]
    public void FormatLong_UsesGroupSeparators()
        => Assert.Equal("693,148", ProbabilityFormatter.FormatLong((long?)693_148, Invariant));

    [Fact]
    public void FormatAverage_NullIsNoData()
        => Assert.Equal("Sin datos", ProbabilityFormatter.FormatAverage(null, Invariant));

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(12.5, "12.5")]
    [InlineData(12.3456, "12.35")]
    public void FormatAverage_Values(double average, string expected)
        => Assert.Equal(expected, ProbabilityFormatter.FormatAverage(average, Invariant));

    [Fact]
    public void FormatProjection_Kinds()
    {
        Assert.Equal("Sin datos", ProbabilityFormatter.FormatProjection(MatchProjection.None, Invariant));
        Assert.Equal("No estimable", ProbabilityFormatter.FormatProjection(MatchProjection.NotEstimable, Invariant));
        Assert.Equal("Fuera de rango", ProbabilityFormatter.FormatProjection(MatchProjection.OutOfRange, Invariant));
        Assert.Equal("1,234", ProbabilityFormatter.FormatProjection(MatchProjection.From(1234), Invariant));
    }
}
