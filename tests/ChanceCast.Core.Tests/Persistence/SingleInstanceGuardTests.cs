using ChanceCast.Core.Infrastructure;

namespace ChanceCast.Core.Tests.Persistence;

public class SingleInstanceGuardTests
{
    private static string UniqueName()
        => @"Local\ChanceCast.Tests." + Guid.NewGuid().ToString("N");

    [Fact]
    public void TryAcquire_FirstInstance_IsPrimary()
    {
        using var guard = SingleInstanceGuard.TryAcquire(UniqueName());

        Assert.True(guard.IsPrimary);
    }

    [Fact]
    public void TryAcquire_SecondInstance_IsNotPrimary()
    {
        var name = UniqueName();
        using var first = SingleInstanceGuard.TryAcquire(name);
        Assert.True(first.IsPrimary);

        // El segundo intento debe ejecutarse en otro hilo: en el mismo hilo el
        // mutex permitiría adquisición recursiva.
        bool? secondIsPrimary = null;
        var thread = new Thread(() =>
        {
            using var second = SingleInstanceGuard.TryAcquire(name);
            secondIsPrimary = second.IsPrimary;
        });
        thread.Start();
        thread.Join();

        Assert.False(secondIsPrimary, "La segunda instancia no debe obtener el acceso.");
    }

    [Fact]
    public void TryAcquire_AfterDispose_AllowsReacquire()
    {
        var name = UniqueName();

        using (var first = SingleInstanceGuard.TryAcquire(name))
        {
            Assert.True(first.IsPrimary);
        }

        using var second = SingleInstanceGuard.TryAcquire(name);
        Assert.True(second.IsPrimary, "Tras liberar el mutex debe poder readquirirse.");
    }

    [Fact]
    public void TryAcquire_AbandonedMutex_IsRecovered()
    {
        var name = UniqueName();

        // Un hilo adquiere el mutex y "muere" sin liberarlo (cierre inesperado).
        var thread = new Thread(() =>
        {
            var leaked = SingleInstanceGuard.TryAcquire(name);
            Assert.True(leaked.IsPrimary);
            // Sin Dispose: el mutex queda abandonado cuando el hilo termina.
        });
        thread.Start();
        thread.Join();

        using var recovered = SingleInstanceGuard.TryAcquire(name);
        Assert.True(recovered.IsPrimary, "Un mutex abandonado debe recuperarse.");
    }

    [Fact]
    public void TryAcquire_EmptyName_Throws()
        => Assert.Throws<ArgumentException>(() => SingleInstanceGuard.TryAcquire(""));

    [Fact]
    public void DefaultMutexName_IsStable()
        => Assert.Equal(@"Local\ChanceCast.SingleInstance", SingleInstanceGuard.DefaultMutexName);
}
