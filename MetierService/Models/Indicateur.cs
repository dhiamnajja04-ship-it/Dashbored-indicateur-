using System.Collections.Generic;

namespace MetierService.Models;

public class Indicateur
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Nom { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Statut { get; set; } = string.Empty;
    public string Unite { get; set; } = string.Empty;
    public string? SourceDeDonner { get; set; }
    public string TypeCollecte { get; set; } = string.Empty;
    public string? Frequence { get; set; }
    public decimal? ValeurCible { get; set; }
    public int? AnneeReference { get; set; }
    public int CategorieId { get; set; }

    public ICollection<ValeurIndicateur> ValeursIndicateurs { get; set; } = new List<ValeurIndicateur>();
}
