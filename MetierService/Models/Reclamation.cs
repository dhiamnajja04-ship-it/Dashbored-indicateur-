using System.ComponentModel.DataAnnotations;

namespace MetierService.Models;

/// <summary>
/// Signalement déposé par un utilisateur sur un indicateur ou sur ses valeurs.
///
/// Une réclamation est purement déclarative : elle ne modifie aucune valeur et
/// n'a donc AUCUN effet sur <c>is_valid</c>. Corriger un chiffre reste une
/// action explicite qui repasse par le workflow de validation.
/// </summary>
public class Reclamation
{
    public int Id { get; set; }

    /// <summary>Indicateur concerné. Null pour une réclamation générale.</summary>
    public int? IndicateurId { get; set; }

    [Required(ErrorMessage = "L'objet de la réclamation est obligatoire.")]
    [StringLength(150, ErrorMessage = "L'objet ne peut pas dépasser 150 caractères.")]
    public string Objet { get; set; } = string.Empty;

    [Required(ErrorMessage = "Le message est obligatoire.")]
    public string Message { get; set; } = string.Empty;

    [Required(ErrorMessage = "Merci d'indiquer qui dépose la réclamation.")]
    [StringLength(100)]
    public string SoumisPar { get; set; } = string.Empty;

    // Pas d'attribut [EmailAddress] ici : il rejette la chaîne vide, alors que
    // le champ est facultatif et qu'un formulaire HTML envoie naturellement ""
    // quand l'utilisateur ne le remplit pas. La validation est faite dans le
    // contrôleur, qui accepte l'absence de valeur et normalise en null.
    [StringLength(150)]
    public string? Email { get; set; }

    public string Statut { get; set; } = StatutReclamation.Nouvelle;

    /// <summary>Réponse apportée par le service traitant.</summary>
    public string? Reponse { get; set; }

    public DateTime CreeLe { get; set; }
    public DateTime? TraiteLe { get; set; }
}

/// <summary>
/// Cycle de vie d'une réclamation.
///
///     Nouvelle ──prendre en charge──▶ EnCours ──▶ Traitee
///        │                               │
///        └─────────── rejeter ───────────┴──────▶ Rejetee
/// </summary>
public static class StatutReclamation
{
    public const string Nouvelle = "Nouvelle";
    public const string EnCours = "EnCours";
    public const string Traitee = "Traitee";
    public const string Rejetee = "Rejetee";

    public static readonly string[] Tous = { Nouvelle, EnCours, Traitee, Rejetee };

    private static readonly Dictionary<string, string[]> Transitions = new(StringComparer.OrdinalIgnoreCase)
    {
        [Nouvelle] = new[] { EnCours, Traitee, Rejetee },
        [EnCours] = new[] { Traitee, Rejetee, Nouvelle },
        [Traitee] = new[] { EnCours },
        [Rejetee] = new[] { EnCours, Nouvelle },
    };

    public static bool EstConnu(string? statut) =>
        statut is not null && Tous.Contains(statut, StringComparer.OrdinalIgnoreCase);

    public static string Normaliser(string statut) =>
        Tous.First(s => s.Equals(statut, StringComparison.OrdinalIgnoreCase));

    public static bool TransitionAutorisee(string? depuis, string vers)
    {
        var origine = EstConnu(depuis) ? Normaliser(depuis!) : Nouvelle;
        if (origine.Equals(vers, StringComparison.OrdinalIgnoreCase)) return true;
        return Transitions.TryGetValue(origine, out var cibles)
            && cibles.Contains(vers, StringComparer.OrdinalIgnoreCase);
    }

    public static string[] TransitionsPossibles(string? depuis)
    {
        var origine = EstConnu(depuis) ? Normaliser(depuis!) : Nouvelle;
        return Transitions.TryGetValue(origine, out var cibles) ? cibles : Array.Empty<string>();
    }
}

/// <summary>Corps attendu par PATCH /api/reclamations/{id}/statut.</summary>
public class ChangementStatutReclamation
{
    public string Statut { get; set; } = string.Empty;
    public string? Reponse { get; set; }
}
