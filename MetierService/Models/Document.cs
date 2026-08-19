namespace MetierService.Models;

/// <summary>
/// Document justificatif attaché à un indicateur (table « meta_data »).
///
/// La table existait dans le schéma initial avec une clé étrangère vers
/// indicateurs, mais n'était pas exploitée. Le fichier lui-même est écrit sur
/// un volume ; la base ne stocke que ses métadonnées et le nom sous lequel il
/// a été enregistré.
/// </summary>
public class Document
{
    public int Id { get; set; }
    public int? IndicateurId { get; set; }
    public string? Description { get; set; }
    public string? SourceDonnee { get; set; }

    /// <summary>Nom d'origine, tel que choisi par l'utilisateur.</summary>
    public string? NomFichier { get; set; }

    /// <summary>Nom sur le disque : un identifiant, jamais le nom d'origine.</summary>
    public string? NomStocke { get; set; }

    public string? TypeMime { get; set; }
    public long? TailleOctets { get; set; }
    public string? DeposePar { get; set; }
    public DateTime? DeposeLe { get; set; }
}

/// <summary>Page de résultats renvoyée par les listes paginées.</summary>
public class PageResultat<T>
{
    public List<T> Elements { get; set; } = new();
    public int Page { get; set; }
    public int Taille { get; set; }
    public int Total { get; set; }
    public int NbPages => Taille > 0 ? (int)Math.Ceiling(Total / (double)Taille) : 0;
}
