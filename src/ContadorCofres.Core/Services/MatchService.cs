using System.IO;
using ContadorCofres.Core.Abstractions;
using ContadorCofres.Core.Models;

namespace ContadorCofres.Core.Services;

/// <summary>Clasifica el motivo del fallo de una operación.</summary>
public enum MatchOperationError
{
    None,

    /// <summary>La operación fue rechazada por una regla de negocio.</summary>
    Validation,

    /// <summary>El guardado falló; el estado en memoria no se confirmó.</summary>
    Save,
}

/// <summary>Resultado de una operación de partidas.</summary>
public sealed record MatchOperationResult(
    bool Success,
    MatchOperationError Error = MatchOperationError.None,
    string? ErrorMessage = null,
    CompletedMatch? FinishedMatch = null)
{
    public static MatchOperationResult Ok(CompletedMatch? finishedMatch = null)
        => new(true, MatchOperationError.None, null, finishedMatch);

    public static MatchOperationResult ValidationFailure(string message)
        => new(false, MatchOperationError.Validation, message);

    public static MatchOperationResult SaveFailure(string message)
        => new(false, MatchOperationError.Save, message);
}

/// <summary>
/// Reglas de partidas: iniciar, contar, finalizar, descartar y editar historial.
/// Cada modificación se guarda como una sola operación lógica y solo se
/// confirma en memoria si el guardado tuvo éxito. Las operaciones se
/// serializan: nunca dos guardados simultáneos.
/// </summary>
public sealed class MatchService
{
    private readonly IAppStateStore _store;
    private readonly IAppLogger _logger;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private AppState _state = AppState.CreateDefault();

    public MatchService(IAppStateStore store, IAppLogger logger, TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Último estado confirmado (guardado o cargado).</summary>
    public AppState Current => _state;

    /// <summary>Se dispara cuando el estado confirmado cambia.</summary>
    public event EventHandler? StateChanged;

    /// <summary>Establece el estado inicial cargado del almacenamiento.</summary>
    public void Initialize(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private DateTimeOffset Now => _timeProvider.GetLocalNow();

    public Task<MatchOperationResult> StartMatchAsync() => MutateAsync("IniciarPartida", state =>
    {
        if (state.ActiveMatch is not null)
            return Reject("Ya hay una partida en curso.");

        var active = new ActiveMatch(Guid.NewGuid(), Now, 0);
        return Accept(state with { ActiveMatch = active });
    });

    public Task<MatchOperationResult> IncrementAsync() => MutateAsync("SumarCofre", state =>
    {
        if (state.ActiveMatch is not { } active)
            return Reject("No hay una partida en curso.");
        if (active.ChestCount == long.MaxValue)
            return Reject("El conteo alcanzó el máximo permitido.");

        return Accept(state with { ActiveMatch = active with { ChestCount = active.ChestCount + 1 } });
    });

    public Task<MatchOperationResult> DecrementAsync() => MutateAsync("RestarCofre", state =>
    {
        if (state.ActiveMatch is not { } active)
            return Reject("No hay una partida en curso.");
        if (active.ChestCount == 0)
            return Reject("El conteo ya está en cero.");

        return Accept(state with { ActiveMatch = active with { ChestCount = active.ChestCount - 1 } });
    });

    public Task<MatchOperationResult> SetCountAsync(long value) => MutateAsync("EditarTotal", state =>
    {
        if (state.ActiveMatch is null)
            return Reject("No hay una partida en curso.");
        if (value < 0)
            return Reject("El conteo no puede ser negativo.");

        return Accept(state with { ActiveMatch = state.ActiveMatch with { ChestCount = value } });
    });

    /// <summary>
    /// Finaliza la partida activa y la agrega al historial como una sola
    /// modificación lógica. Si no hay partida activa falla, lo que también
    /// evita duplicados por doble clic o ejecución repetida.
    /// </summary>
    public Task<MatchOperationResult> FinishMatchAsync() => MutateAsync("FinalizarPartida", state =>
    {
        if (state.ActiveMatch is not { } active)
            return Reject("No hay una partida en curso.");

        var finishedAt = Now;
        if (finishedAt < active.StartedAt)
            finishedAt = active.StartedAt; // reloj del sistema hacia atrás

        var completed = new CompletedMatch(active.Id, active.StartedAt, finishedAt, active.ChestCount);
        var matches = new List<CompletedMatch>(state.Matches) { completed };
        return new Mutation(state with { ActiveMatch = null, Matches = matches }, FinishedMatch: completed);
    });

    /// <summary>Elimina la partida activa sin crear historial.</summary>
    public Task<MatchOperationResult> DiscardMatchAsync() => MutateAsync("DescartarPartida", state =>
    {
        if (state.ActiveMatch is null)
            return Reject("No hay una partida en curso.");

        return Accept(state with { ActiveMatch = null });
    });

    public Task<MatchOperationResult> EditHistoryAsync(Guid matchId, long newChestCount) => MutateAsync("EditarHistorial", state =>
    {
        var index = IndexOfMatch(state.Matches, matchId);
        if (index < 0)
            return Reject("El registro ya no existe en el historial.");
        if (newChestCount < 0)
            return Reject("El conteo no puede ser negativo.");

        var matches = new List<CompletedMatch>(state.Matches);
        matches[index] = matches[index] with { ChestCount = newChestCount };
        return Accept(state with { Matches = matches });
    });

    public Task<MatchOperationResult> DeleteHistoryAsync(Guid matchId) => MutateAsync("EliminarHistorial", state =>
    {
        var index = IndexOfMatch(state.Matches, matchId);
        if (index < 0)
            return Reject("El registro ya no existe en el historial.");

        var matches = new List<CompletedMatch>(state.Matches);
        matches.RemoveAt(index);
        return Accept(state with { Matches = matches });
    });

    /// <summary>
    /// Reemplaza la configuración completa. El cambio de denominador afecta
    /// todos los cálculos retroactivamente (los perfiles históricos quedan
    /// fuera del alcance de esta versión).
    /// </summary>
    public Task<MatchOperationResult> UpdateSettingsAsync(AppSettings settings) => MutateAsync("ActualizarConfiguracion", state =>
    {
        try
        {
            ModelValidation.ValidateSettings(settings);
        }
        catch (InvalidDataException ex)
        {
            return Reject(ex.Message);
        }

        return Accept(state with { Settings = settings });
    });

    private static int IndexOfMatch(IReadOnlyList<CompletedMatch> matches, Guid id)
    {
        for (var i = 0; i < matches.Count; i++)
            if (matches[i].Id == id)
                return i;
        return -1;
    }

    private static Mutation Accept(AppState candidate, CompletedMatch? finishedMatch = null)
        => new(candidate, Rejection: null, FinishedMatch: finishedMatch);

    private static Mutation Reject(string message)
        => new(Candidate: null!, Rejection: MatchOperationResult.ValidationFailure(message));

    private async Task<MatchOperationResult> MutateAsync(string operation, Func<AppState, Mutation> mutate)
    {
        await _gate.WaitAsync();
        try
        {
            var mutation = mutate(_state);
            if (mutation.Rejection is { } rejection)
            {
                _logger.Warn(operation, rejection.ErrorMessage);
                return rejection;
            }

            SaveResult save;
            try
            {
                save = await _store.SaveAsync(mutation.Candidate);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Error(operation, "Excepción al guardar el estado.", ex);
                return MatchOperationResult.SaveFailure(
                    "No se pudieron guardar los cambios. El conteo no fue actualizado. " +
                    "Revisá que la carpeta de datos esté disponible e intentá nuevamente.");
            }

            if (!save.Success)
            {
                _logger.Error(operation, save.ErrorMessage);
                return MatchOperationResult.SaveFailure(
                    save.ErrorMessage ?? "No se pudieron guardar los cambios. El conteo no fue actualizado.");
            }

            // El guardado tuvo éxito: recién ahora se confirma el estado en memoria.
            _state = mutation.Candidate;
            _logger.Info(operation);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return MatchOperationResult.Ok(mutation.FinishedMatch);
        }
        finally
        {
            _gate.Release();
        }
    }

    private sealed record Mutation(AppState Candidate, MatchOperationResult? Rejection = null, CompletedMatch? FinishedMatch = null);
}
