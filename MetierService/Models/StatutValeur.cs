namespace MetierService.Models;

/// <summary>
/// Workflow de validation d'une valeur d'indicateur (semaine 8).
///
///     Brouillon  ──soumettre──▶  EnRevue  ──valider──▶  Valide
///         ▲                         │                      │
///         └──────rejeter◀───────────┘                      │
///         └──────────────dévalider───────────────────────-─┘
///
/// Seul l'état <see cref="Valide"/> met is_valid = true en base, et seules
/// les valeurs is_valid = true sont transmises au service IA.
/// </summary>
public static class StatutValeur
{
    public const string Brouillon = "Brouillon";
    public const string EnRevue = "EnRevue";
    public const string Valide = "Valide";
    public const string Rejete = "Rejete";

    public static readonly string[] Tous = { Brouillon, EnRevue, Valide, Rejete };

    /// <summary>Transitions autorisées. Toute autre transition est refusée en 400.</summary>
    private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Brouillon] = new[] { EnRevue, Valide },
        [EnRevue] = new[] { Valide, Rejete, Brouillon },
        [Valide] = new[] { Brouillon, EnRevue },
        [Rejete] = new[] { Brouillon, EnRevue },
    };

    public static bool EstConnu(string? statut) =>
        statut is not null && Tous.Contains(statut, StringComparer.OrdinalIgnoreCase);

    /// <summary>Normalise la casse pour éviter « valide » / « Valide » en base.</summary>
    public static string Normaliser(string statut) =>
        Tous.First(s => s.Equals(statut, StringComparison.OrdinalIgnoreCase));

    public static bool TransitionAutorisee(string? depuis, string vers)
    {
        // Une valeur dont le statut est vide ou inconnu (données historiques) est
        // traitée comme un brouillon : on ne bloque pas la reprise de l'existant.
        var origine = EstConnu(depuis) ? Normaliser(depuis!) : Brouillon;
        if (origine.Equals(vers, StringComparison.OrdinalIgnoreCase))
        {
            return true; // idempotent
        }
        return Transitions.TryGetValue(origine, out var cibles)
            && cibles.Contains(vers, StringComparer.OrdinalIgnoreCase);
    }

    public static string[] TransitionsPossibles(string? depuis)
    {
        var origine = EstConnu(depuis) ? Normaliser(depuis!) : Brouillon;
        return Transitions.TryGetValue(origine, out var cibles) ? cibles : Array.Empty<string>();
    }
}
