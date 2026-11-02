using System.Text;
using ChanceCast.Core.Services;

namespace ChanceCast.Core.Tests.Persistence;

public class CsvExporterTests
{
    [Fact]
    public void Export_NoMatches_ContainsOnlyHeader()
    {
        var csv = CsvExporter.Export([]);

        Assert.Equal(CsvExporter.Header, csv.TrimEnd('\r', '\n'));
    }

    [Fact]
    public void Export_WritesHeaderAndRows()
    {
        var match = TestData.Match(12);

        var csv = CsvExporter.Export([match]);
        var lines = csv.Split("\r\n");

        Assert.Equal(CsvExporter.Header, lines[0]);
        Assert.Equal(2, lines.Length);
        var fields = lines[1].Split(',');
        Assert.Equal(4, fields.Length);
        Assert.Equal(match.Id.ToString("D"), fields[0]);
        Assert.Equal("12", fields[3]);
        Assert.Contains("2026-01-01T10:00:00", fields[1]); // fecha ISO de ida y vuelta
    }

    [Fact]
    public void Export_OrdersByStartDate()
    {
        var later = TestData.Match(1, TestData.BaseStart.AddHours(2));
        var earlier = TestData.Match(2, TestData.BaseStart);

        var csv = CsvExporter.Export([later, earlier]);

        var lines = csv.Split("\r\n");
        Assert.Contains(earlier.Id.ToString("D"), lines[1]);
        Assert.Contains(later.Id.ToString("D"), lines[2]);
    }

    [Theory]
    [InlineData("texto simple", "texto simple")]
    [InlineData("con,coma", "\"con,coma\"")]
    [InlineData("con \"comillas\"", "\"con \"\"comillas\"\"\"")]
    [InlineData("texto", "texto")]
    public void Escape_AppliesRfc4180Rules(string field, string expected)
        => Assert.Equal(expected, CsvExporter.Escape(field));

    [Fact]
    public void Escape_EscapesLineBreaks()
        => Assert.Equal("\"línea\nnueva\"", CsvExporter.Escape("línea\nnueva"));

    [Fact]
    public void ToUtf8Bytes_StartsWithBomAndRoundTrips()
    {
        const string csv = "ID,Inicio,Finalización,Intentos\r\ncafé,ñandú,1,2";

        var bytes = CsvExporter.ToUtf8Bytes(csv);

        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        Assert.Equal(csv, Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }
}
