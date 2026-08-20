namespace IaService.Models;

/// <summary>
/// Vue « lecture seule » d'un indicateur telle que renvoyée par le service métier
/// sur /api/indicators/validated. Le service IA ne touche jamais PostgreSQL :
/// il ne connaît que ce contrat HTTP (architecture validée en semaine 1).
/// </summary>
public class IndicateurValide
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Unite { get; set; } = string.Empty;
    public string? Frequence { get; set; }
    public decimal? ValeurCible { get; set; }
    public int? AnneeReference { get; set; }

    /// <summary>Uniquement les valeurs dont is_valid = true (filtrage fait côté métier).</summary>
    public List<ValeurValide> Valeurs { get; set; } = new();
}

public class ValeurValide
{
    public int Id { get; set; }
    public decimal Valeur { get; set; }
    public string? Pays { get; set; }
    public string? Gouvernorat { get; set; }
    public string? OrganisationNom { get; set; }
    public string? PeriodeLibelle { get; set; }
    public string? Statut { get; set; }
    public string? DegreDeFiabilite { get; set; }
    public string? Commentaire { get; set; }
    public DateTime SaisieLe { get; set; }
    public string? ValidePar { get; set; }
    public int OrganisationId { get; set; }
    public int PeriodeId { get; set; }
}
