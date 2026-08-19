namespace MetierService.Models;

/// <summary>Corps des endpoints de validation / changement de statut.</summary>
public class ChangementStatutRequest
{
    /// <summary>Statut cible : Brouillon, EnRevue, Valide ou Rejete.</summary>
    public string Statut { get; set; } = string.Empty;

    /// <summary>Qui effectue l'action (tracé dans valide_par pour une validation).</summary>
    public string? Utilisateur { get; set; }

    /// <summary>Motif, notamment en cas de rejet.</summary>
    public string? Commentaire { get; set; }
}

/// <summary>Corps de PATCH .../validate et .../devalidate.</summary>
public class ValidationRequest
{
    public string? Utilisateur { get; set; }
    public string? Commentaire { get; set; }
}

/// <summary>Vue d'un indicateur ne contenant QUE ses valeurs validées (consommée par le service IA).</summary>
public class IndicateurValideDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unite { get; set; } = string.Empty;
    public string? Frequence { get; set; }
    public decimal? ValeurCible { get; set; }
    public int? AnneeReference { get; set; }
    public List<ValeurValideDto> Valeurs { get; set; } = new();
}

public class ValeurValideDto
{
    public int Id { get; set; }
    public decimal Valeur { get; set; }
    public string? Statut { get; set; }
    public string? DegreDeFiabilite { get; set; }
    public string? Commentaire { get; set; }
    public DateTime SaisieLe { get; set; }
    public string? ValidePar { get; set; }
    public int OrganisationId { get; set; }
    public int PeriodeId { get; set; }
}
