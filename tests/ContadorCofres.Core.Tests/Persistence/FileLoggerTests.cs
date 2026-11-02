using System.Text;
using ContadorCofres.Core.Infrastructure;

namespace ContadorCofres.Core.Tests.Persistence;

public class FileLoggerTests : IDisposable
{
    private readonly TempDir _dir = new();

    public void Dispose() => _dir.Dispose();

    private string LogPath => Path.Combine(_dir.Path, "app.log");

    private FileLogger CreateLogger(long maxBytes = 1_048_576, int rotated = 3)
        => new(_dir.Path, maxBytes, rotated);

    [Fact]
    public void Write_RecordsLevelOperationAndDetail()
    {
        var logger = CreateLogger();

        logger.Info("Prueba", "detalle informativo");

        var content = File.ReadAllText(LogPath);
        Assert.Contains("[INFO]", content);
        Assert.Contains("Prueba", content);
        Assert.Contains("detalle informativo", content);
    }

    [Fact]
    public void Write_RecordsWarnAndErrorLevels()
    {
        var logger = CreateLogger();

        logger.Warn("Operación");
        logger.Error("Otra operación", "falló");

        var content = File.ReadAllText(LogPath);
        Assert.Contains("[WARN] Operación", content);
        Assert.Contains("[ERROR] Otra operación: falló", content);
    }

    [Fact]
    public void Error_IncludesExceptionMessageAndStackTrace()
    {
        var logger = CreateLogger();

        InvalidOperationException exception;
        try
        {
            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException caught)
        {
            exception = caught;
        }

        logger.Error("Guardado", "no se pudo escribir", exception);

        var content = File.ReadAllText(LogPath);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("boom", content);
        Assert.Contains("   at ", content); // stack trace
    }

    [Fact]
    public void Write_RotatesWhenExceedingMaxSize()
    {
        var logger = CreateLogger(maxBytes: 2048, rotated: 3);

        for (var i = 0; i < 200; i++)
            logger.Info($"Operación número {i} con texto suficientemente largo para ocupar varios bytes en cada línea del registro.");

        Assert.True(File.Exists(LogPath), "El archivo activo debe existir.");
        Assert.True(File.Exists(LogPath + ".1"), "Debe existir la primera rotación.");
        Assert.True(File.Exists(LogPath + ".3"), "Debe existir la última rotación conservada.");
        Assert.False(File.Exists(LogPath + ".4"), "No deben conservarse más rotaciones que las configuradas.");
        Assert.True(new FileInfo(LogPath).Length <= 2048, "El archivo activo debe quedar por debajo del límite tras rotar.");
    }

    [Fact]
    public void Constructor_WithInvalidDirectory_DoesNotThrow()
    {
        // Una ruta de carpeta ocupada por un archivo no debe romper la aplicación.
        var filePath = Path.Combine(_dir.Path, "ocupado");
        File.WriteAllText(filePath, "no soy una carpeta");

        var logger = new FileLogger(filePath);
        logger.Info("Prueba"); // se descarta silenciosamente
    }

    [Fact]
    public void Write_ConcurrentCalls_DoNotLoseLinesOrCorruptTheFile()
    {
        var logger = CreateLogger(maxBytes: 64_000);

        Parallel.For(0, 100, i => logger.Info("Concurrente", $"línea {i}"));

        var content = File.ReadAllText(LogPath);
        var count = content.Split('\n').Count(l => l.Contains("[INFO] Concurrente"));
        Assert.Equal(100, count);
    }

    [Fact]
    public void Write_HandlesTextInSpanish()
    {
        var logger = CreateLogger();

        logger.Info("Operación", "café, ñandú, corazón");

        Assert.Contains("café, ñandú, corazón", File.ReadAllText(LogPath, Encoding.UTF8));
    }
}
