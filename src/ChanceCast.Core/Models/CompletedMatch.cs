namespace ChanceCast.Core.Models;

/// <summary>
/// Sesión finalizada registrada en el historial.
/// La fecha de finalización nunca es anterior a la de inicio.
/// </summary>
public sealed record CompletedMatch(Guid Id, DateTimeOffset StartedAt, DateTimeOffset FinishedAt, long Attempts);
