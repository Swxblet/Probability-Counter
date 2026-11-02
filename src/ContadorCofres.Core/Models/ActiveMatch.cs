namespace ContadorCofres.Core.Models;

/// <summary>
/// Partida en curso. Solo puede existir una a la vez y su conteo nunca es negativo.
/// </summary>
public sealed record ActiveMatch(Guid Id, DateTimeOffset StartedAt, long ChestCount);
