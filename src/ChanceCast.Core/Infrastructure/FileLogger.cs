namespace ChanceCast.Core.Infrastructure;

/// <summary>
/// Registro de eventos en archivo con rotación por tamaño.
/// El logger nunca lanza excepciones: los fallos de escritura se descartan.
/// </summary>
public sealed class FileLogger : Abstractions.IAppLogger
{
    private readonly object _sync = new();
    private readonly string _logFilePath;
    private readonly long _maxBytes;
    private readonly int _rotatedFilesKept;

    /// <param name="logDirectory">Carpeta donde se crea app.log.</param>
    /// <param name="maxBytesPerFile">Tamaño máximo antes de rotar (mínimo 1024).</param>
    /// <param name="rotatedFilesKept">Cantidad de archivos rotados conservados (app.log.1, app.log.2, …).</param>
    public FileLogger(string logDirectory, long maxBytesPerFile = 1_048_576, int rotatedFilesKept = 3)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            throw new ArgumentException("La carpeta de logs no puede estar vacía.", nameof(logDirectory));

        try
        {
            Directory.CreateDirectory(logDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Sin carpeta de logs la aplicación sigue funcionando.
        }

        _logFilePath = Path.Combine(logDirectory, "app.log");
        _maxBytes = Math.Max(1024, maxBytesPerFile);
        _rotatedFilesKept = Math.Max(1, rotatedFilesKept);
    }

    public void Info(string operation, string? detail = null) => Write("INFO", operation, detail, null);

    public void Warn(string operation, string? detail = null, Exception? exception = null) => Write("WARN", operation, detail, exception);

    public void Error(string operation, string? detail = null, Exception? exception = null) => Write("ERROR", operation, detail, exception);

    private void Write(string level, string operation, string? detail, Exception? exception)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz");
        var line = $"[{timestamp}] [{level}] {operation}"
            + (string.IsNullOrEmpty(detail) ? string.Empty : ": " + detail)
            + (exception is null ? string.Empty : Environment.NewLine + exception);

        lock (_sync)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(_logFilePath, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                // Registrar no debe interrumpir la operación principal.
            }
        }
    }

    private void RotateIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath))
                return;
            if (new FileInfo(_logFilePath).Length < _maxBytes)
                return;

            for (var i = _rotatedFilesKept; i >= 1; i--)
            {
                var source = i == 1 ? _logFilePath : PathOf(i - 1);
                var target = PathOf(i);
                if (File.Exists(source))
                    File.Copy(source, target, overwrite: true);
            }

            File.WriteAllText(_logFilePath, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Si la rotación falla, se intenta escribir igual en el archivo activo.
        }
    }

    private string PathOf(int index) => _logFilePath + "." + index;
}
