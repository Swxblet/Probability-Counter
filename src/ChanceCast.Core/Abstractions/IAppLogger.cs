namespace ChanceCast.Core.Abstractions;

/// <summary>Registro de eventos de la aplicación.</summary>
public interface IAppLogger
{
    void Info(string operation, string? detail = null);

    void Warn(string operation, string? detail = null, Exception? exception = null);

    void Error(string operation, string? detail = null, Exception? exception = null);
}

/// <summary>Logger que descarta todo. Útil en pruebas.</summary>
public sealed class NullLogger : IAppLogger
{
    public static NullLogger Instance { get; } = new();

    public void Info(string operation, string? detail = null)
    {
    }

    public void Warn(string operation, string? detail = null, Exception? exception = null)
    {
    }

    public void Error(string operation, string? detail = null, Exception? exception = null)
    {
    }
}
