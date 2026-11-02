using ChanceCast.App.Abstractions;
using ChanceCast.Core.Abstractions;
using ChanceCast.Core.Models;
using ChanceCast.Core.Services;

namespace ChanceCast.App.Services;

/// <summary>Resultado de una exportación a CSV.</summary>
public sealed record CsvExportOutcome(bool Success, bool Cancelled, string? ErrorMessage = null);

/// <summary>
/// Exportación del historial a CSV. Pide la ruta mediante un diálogo y
/// escribe UTF-8 con BOM para texto en español. Cancelar no es un error.
/// </summary>
public sealed class CsvExportService(IDialogService dialogs, IAppLogger logger)
{
    private readonly IDialogService _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
    private readonly IAppLogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CsvExportOutcome> ExportAsync(IReadOnlyList<CompletedMatch> matches, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matches);

        if (matches.Count == 0)
            return new CsvExportOutcome(false, false, "No hay sesiones en el historial para exportar.");

        var fileName = $"chancecast-{DateTime.Now:yyyyMMdd-HHmm}.csv";
        var path = await _dialogs.GetCsvSavePathAsync(fileName);
        if (path is null)
            return new CsvExportOutcome(false, true);

        try
        {
            await File.WriteAllBytesAsync(path, CsvExporter.ToUtf8Bytes(CsvExporter.Export(matches)), cancellationToken);
            _logger.Info("ExportarCsv", $"{matches.Count} sesiones exportadas.");
            return new CsvExportOutcome(true, false);
        }
        catch (OperationCanceledException)
        {
            return new CsvExportOutcome(false, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Error("ExportarCsv", "No se pudo escribir el archivo CSV.", ex);
            return new CsvExportOutcome(false, false, "No se pudo exportar el archivo. Revisá la carpeta de destino e intentá nuevamente.");
        }
    }
}
