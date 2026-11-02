namespace ChanceCast.Core.Models;

/// <summary>
/// Sesión en curso. Solo puede existir una a la vez y su conteo nunca es negativo.
/// </summary>
public sealed record ActiveMatch(Guid Id, DateTimeOffset StartedAt, long Attempts);
