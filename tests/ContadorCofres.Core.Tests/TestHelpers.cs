using ContadorCofres.Core.Abstractions;
using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Tests;

/// <summary>Almacén en memoria para pruebas del servicio.</summary>
public sealed class InMemoryStore : IAppStateStore
{
    public AppState? SavedState { get; private set; }
    public int SaveCalls { get; private set; }
    public bool FailNextSave { get; set; }

    public Task<SaveResult> SaveAsync(AppState state, CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        if (FailNextSave)
        {
            FailNextSave = false;
            return Task.FromResult(SaveResult.Fail("fallo simulado al guardar"));
        }

        SavedState = state;
        return Task.FromResult(SaveResult.Ok());
    }

    public AppStateLoadResult Load() => AppStateLoadResult.NoData("sin datos", filesExisted: false);
}

/// <summary>Reloj controlable para probar reglas de fechas.</summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _current = new(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(-3));

    public override DateTimeOffset GetUtcNow() => _current.ToUniversalTime();

    public void Set(DateTimeOffset value) => _current = value;

    public void Advance(TimeSpan time) => _current += time;
}

/// <summary>Carpeta temporal por prueba; se elimina al disponer.</summary>
public sealed class TempDir : IDisposable
{
    public string Path { get; }

    public TempDir(string? subDirectory = null)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "contador-cofres-tests-" + Guid.NewGuid().ToString("N"));
        if (subDirectory is not null)
            Path = System.IO.Path.Combine(Path, subDirectory);
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Las carpetas de prueba son desechables; si queda algo, el sistema la borra después.
        }
    }
}

public static class TestData
{
    public static readonly DateTimeOffset BaseStart = new(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-3));

    public static CompletedMatch Match(
        long chests,
        DateTimeOffset? startedAt = null,
        DateTimeOffset? finishedAt = null,
        Guid? id = null)
    {
        var start = startedAt ?? BaseStart;
        return new CompletedMatch(id ?? Guid.NewGuid(), start, finishedAt ?? start.AddMinutes(20), chests);
    }

    public static AppState StateWithMatches(params long[] chestCounts)
    {
        var matches = chestCounts
            .Select((chests, i) => Match(chests, BaseStart.AddMinutes(30 * i)))
            .ToList();
        return new AppState { Matches = matches };
    }
}
