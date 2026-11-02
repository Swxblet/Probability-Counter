namespace ContadorCofres.Core.Models;

/// <summary>
/// Estado completo de la aplicación. Los valores derivados (totales, promedios,
/// probabilidades) no se persisten: se calculan a partir de los registros.
/// </summary>
public sealed record AppState
{
    public AppSettings Settings { get; init; } = AppSettings.CreateDefault();
    public ActiveMatch? ActiveMatch { get; init; }
    public IReadOnlyList<CompletedMatch> Matches { get; init; } = [];

    public static AppState CreateDefault() => new();
}
