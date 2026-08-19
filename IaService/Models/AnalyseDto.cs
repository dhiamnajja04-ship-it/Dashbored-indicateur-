namespace IaService.Models;

/// <summary>Corps de requête (optionnel) de POST /api/ia/analyse.</summary>
public class AnalyseRequest
{
    /// <summary>Question libre de l'utilisateur. Si vide, on demande une synthèse générale.</summary>
    public string? Question { get; set; }

    /// <summary>Limiter l'analyse à un seul indicateur (son id). Null = tous les indicateurs validés.</summary>
    public int? IndicateurId { get; set; }
}

/// <summary>Réponse renvoyée au frontend (via le Gateway).</summary>
public class AnalyseResponse
{
    /// <summary>Le texte généré par le modèle local.</summary>
    public string Reponse { get; set; } = string.Empty;

    /// <summary>Nom du modèle Ollama utilisé (traçabilité de la démo semaine 8).</summary>
    public string Modele { get; set; } = string.Empty;

    /// <summary>Nombre d'indicateurs réellement transmis au modèle.</summary>
    public int NbIndicateursAnalyses { get; set; }

    /// <summary>Nombre de valeurs validées transmises au modèle.</summary>
    public int NbValeursValidees { get; set; }

    /// <summary>
    /// Codes des indicateurs transmis au modèle. Permet de vérifier en démo que
    /// l'IA n'a bien vu que les indicateurs validés.
    /// </summary>
    public List<string> IndicateursUtilises { get; set; } = new();

    public DateTime GenereLe { get; set; } = DateTime.UtcNow;
}
