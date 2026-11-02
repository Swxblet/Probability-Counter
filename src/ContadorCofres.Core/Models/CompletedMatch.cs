namespace ContadorCofres.Core.Models;

/// <summary>
/// Partida finalizada registrada en el historial.
/// La fecha de finalización nunca es anterior a la de inicio.
/// </summary>
public sealed record CompletedMatch(Guid Id, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, long ChestCount);
