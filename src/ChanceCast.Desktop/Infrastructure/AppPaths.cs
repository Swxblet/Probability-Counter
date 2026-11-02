using System.IO;

namespace ChanceCast.Desktop.Infrastructure;

/// <summary>
/// Rutas de datos en %LOCALAPPDATA%\ChanceCast. Nunca se guarda junto al
/// ejecutable ni en carpetas temporales de extracción.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "ChanceCast";
    public const string DataFileName = "data.json";
    public const string BackupFileName = "data.backup.json";
    public const string LogsFolderName = "logs";

    public static string BaseDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        FolderName);

    public static string DataFile { get; } = Path.Combine(BaseDirectory, DataFileName);

    public static string BackupFile { get; } = Path.Combine(BaseDirectory, BackupFileName);

    public static string LogsDirectory { get; } = Path.Combine(BaseDirectory, LogsFolderName);

    public static void EnsureCreated() => Directory.CreateDirectory(BaseDirectory);
}
