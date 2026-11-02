using System.Globalization;
using System.Text;
using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Services;

/// <summary>
/// Generación del CSV del historial (RFC 4180). Las fechas se exportan en
/// formato de ida y vuelta (ISO 8601) para no perder la zona horaria.
/// </summary>
public static class CsvExporter
{
    public const string Header = "ID,Inicio,Finalización,Cofres";

    /// <summary>Exporta las partidas finalizadas ordenadas por fecha de inicio.</summary>
    /// <remarks>
    /// Usa CRLF explícito (RFC 4180) en lugar de <c>AppendLine</c>, cuyo
    /// salto depende de la plataforma, para que el archivo sea idéntico en
    /// Windows y Linux y lo abra bien Excel.
    /// </remarks>
    public static string Export(IEnumerable<CompletedMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var builder = new StringBuilder(Header);
        foreach (var match in matches.OrderBy(m => m.StartedAt))
        {
            builder.Append("\r\n");
            builder.Append(Escape(match.Id.ToString("D")));
            builder.Append(',');
            builder.Append(Escape(match.StartedAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Escape(match.FinishedAt.ToString("O", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Escape(match.ChestCount.ToString(CultureInfo.InvariantCulture)));
        }

        return builder.ToString();
    }

    /// <summary>Convierte el CSV a bytes UTF-8 con BOM para texto en español.</summary>
    public static byte[] ToUtf8Bytes(string csv)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(csv);
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }

    /// <summary>Escapa un campo según RFC 4180.</summary>
    public static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }
}
