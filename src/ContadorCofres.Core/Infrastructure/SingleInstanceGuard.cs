using System.Threading;

namespace ContadorCofres.Core.Infrastructure;

/// <summary>
/// Garantiza una única instancia con acceso de escritura mediante un Mutex con
/// nombre. El prefijo "Local\" limita el alcance a la sesión del usuario actual.
/// Si la instancia anterior cerró de forma inesperada, el mutex abandonado se
/// recupera automáticamente.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string DefaultMutexName = @"Local\ContadorCofres.SingleInstance";

    private Mutex? _mutex;

    /// <summary>Indica si esta instancia obtuvo el acceso exclusivo.</summary>
    public bool IsPrimary { get; }

    private SingleInstanceGuard(Mutex? mutex, bool isPrimary)
    {
        _mutex = mutex;
        IsPrimary = isPrimary;
    }

    /// <summary>
    /// Intenta adquirir el acceso exclusivo. La segunda instancia debe informar
    /// al usuario y no escribir datos.
    /// </summary>
    public static SingleInstanceGuard TryAcquire(string mutexName = DefaultMutexName)
    {
        if (string.IsNullOrWhiteSpace(mutexName))
            throw new ArgumentException("El nombre del mutex no puede estar vacío.", nameof(mutexName));

        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (createdNew)
            return new SingleInstanceGuard(mutex, isPrimary: true);

        try
        {
            // El mutex ya existía: solo se obtiene si fue abandonado por un cierre
            // inesperado o liberado por la instancia anterior.
            if (mutex.WaitOne(TimeSpan.Zero, exitContext: false))
                return new SingleInstanceGuard(mutex, isPrimary: true);
        }
        catch (AbandonedMutexException)
        {
            // Adquirir un mutex abandonado significa que la instancia anterior murió.
            return new SingleInstanceGuard(mutex, isPrimary: true);
        }

        mutex.Dispose();
        return new SingleInstanceGuard(mutex: null, isPrimary: false);
    }

    public void Dispose()
    {
        var mutex = _mutex;
        if (mutex is null) return;
        _mutex = null;

        try
        {
            if (IsPrimary)
                mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // El hilo actual no era el propietario: nada que liberar.
        }
        finally
        {
            mutex.Dispose();
        }
    }
}
