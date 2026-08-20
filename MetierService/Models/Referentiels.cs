namespace MetierService.Models;

/// <summary>
/// Organisation productrice de données (table « organisations »).
/// La table portait déjà une hiérarchie via id_parent ; elle n'était
/// simplement jamais exposée, obligeant l'interface à demander un identifiant
/// numérique saisi à la main.
/// </summary>
public class Organisation
{
    public int Id { get; set; }
    public string? Nom { get; set; }
    public string? NiveauAdministratif { get; set; }
    public int? IdParent { get; set; }
}

/// <summary>
/// Période de référence d'une mesure (table « periodes »).
/// Les bornes sont de vraies dates : l'interface peut donc proposer un choix
/// par date plutôt qu'un identifiant.
/// </summary>
public class Periode
{
    public int Id { get; set; }
    public int? Annee { get; set; }
    public string? Libelle { get; set; }
    public string? TypePeriode { get; set; }
    public DateOnly? DateDebut { get; set; }
    public DateOnly? DateFin { get; set; }
}
