using System.IO;

namespace ChanceCast.Core.Models;

/// <summary>
/// Validación de invariantes del modelo, comsesión por el servicio y el
/// almacenamiento.
/// </summary>
public static class ModelValidation
{
    private const int KnownModifiersMask = 0xF;

    public static void ValidateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.DropDenominator < 1)
            throw new InvalidDataException("El denominador debe ser mayor o igual a 1.");
        if (settings.DropDenominator > long.MaxValue)
            throw new InvalidDataException("El denominador está fuera del rango permitido.");
    }

    public static void ValidateHotkeyBinding(HotkeyBinding? binding)
    {
        if (binding is null) return;
        if (string.IsNullOrWhiteSpace(binding.Key))
            throw new InvalidDataException("Un atajo configurado tiene la tecla vacía.");
        if (((int)binding.Modifiers & ~KnownModifiersMask) != 0)
            throw new InvalidDataException("Un atajo configurado tiene modificadores desconocidos.");
    }

    public static void ValidateActiveMatch(ActiveMatch match)
    {
        if (match.Id == Guid.Empty)
            throw new InvalidDataException("La sesión activa no tiene identificador.");
        if (match.Attempts < 0)
            throw new InvalidDataException("El conteo de la sesión activa no puede ser negativo.");
    }

    public static void ValidateCompletedMatch(CompletedMatch match)
    {
        if (match.Id == Guid.Empty)
            throw new InvalidDataException("Un registro del historial no tiene identificador.");
        if (match.Attempts < 0)
            throw new InvalidDataException("El conteo de un registro no puede ser negativo.");
        if (match.FinishedAt < match.StartedAt)
            throw new InvalidDataException("La fecha de finalización no puede ser anterior a la de inicio.");
    }

    /// <summary>Valida una lista de sesiones finalizadas y rechaza identificadores duplicados.</summary>
    public static void ValidateMatches(IReadOnlyList<CompletedMatch> matches)
    {
        var seen = new HashSet<Guid>();
        foreach (var match in matches)
        {
            ValidateCompletedMatch(match);
            if (!seen.Add(match.Id))
                throw new InvalidDataException("El historial contiene registros duplicados.");
        }
    }
}
