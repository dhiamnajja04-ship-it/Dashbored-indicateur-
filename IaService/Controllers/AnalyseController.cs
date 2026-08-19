using IaService.Models;
using IaService.Services;
using Microsoft.AspNetCore.Mvc;

namespace IaService.Controllers;

[Route("api/ia")]
[ApiController]
public class AnalyseController : ControllerBase
{
    private readonly MetierClient _metier;
    private readonly OllamaClient _ollama;
    private readonly ILogger<AnalyseController> _logger;

    public AnalyseController(
        MetierClient metier,
        OllamaClient ollama,
        ILogger<AnalyseController> logger
    )
    {
        _metier = metier;
        _ollama = ollama;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/ia/analyse
    /// Récupère les indicateurs VALIDÉS auprès du service métier, construit le prompt
    /// et interroge le modèle local. Les valeurs non validées ne sont jamais transmises.
    /// </summary>
    [HttpPost("analyse")]
    public async Task<ActionResult<AnalyseResponse>> Analyser(
        [FromBody] AnalyseRequest? requete,
        CancellationToken ct
    )
    {
        requete ??= new AnalyseRequest();

        List<IndicateurValide> indicateurs;
        try
        {
            indicateurs = await _metier.GetIndicateursValidesAsync(requete.IndicateurId, ct);
        }
        catch (MetierIndisponibleException ex)
        {
            return StatusCode(502, new { message = $"Impossible de récupérer les indicateurs. {ex.Message}" });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Service métier injoignable");
            return StatusCode(
                502,
                new { message = "Le service métier est injoignable. L'analyse IA est impossible." }
            );
        }

        // Deuxième filet de sécurité : même si le métier renvoyait des valeurs non
        // validées, elles seraient écartées ici avant construction du prompt.
        var indicateursUtiles = indicateurs
            .Where(i => i.Valeurs.Count > 0)
            .ToList();

        var nbValeurs = indicateursUtiles.Sum(i => i.Valeurs.Count);

        if (nbValeurs == 0)
        {
            // Pas de données validées => on n'appelle même pas le modèle : cela évite
            // qu'il « meuble » avec du texte inventé (exigence semaine 5).
            return Ok(
                new AnalyseResponse
                {
                    Reponse =
                        "Aucun indicateur validé n'est disponible pour le moment. "
                        + "Validez au moins une valeur d'indicateur pour lancer une analyse.",
                    Modele = _ollama.Modele,
                    NbIndicateursAnalyses = 0,
                    NbValeursValidees = 0,
                }
            );
        }

        var prompt = PromptBuilder.Construire(indicateursUtiles, requete.Question);
        _logger.LogInformation(
            "Analyse IA sur {NbIndicateurs} indicateur(s) et {NbValeurs} valeur(s) validée(s)",
            indicateursUtiles.Count,
            nbValeurs
        );

        string texte;
        try
        {
            texte = await _ollama.GenererAsync(prompt, ct);
        }
        catch (ModeleIndisponibleException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Ollama injoignable");
            return StatusCode(
                503,
                new { message = "Le modèle IA local est injoignable. Vérifie qu'Ollama tourne." }
            );
        }
        catch (TaskCanceledException)
        {
            return StatusCode(
                504,
                new { message = "Le modèle IA a mis trop de temps à répondre. Réessaie." }
            );
        }

        return Ok(
            new AnalyseResponse
            {
                Reponse = texte,
                Modele = _ollama.Modele,
                NbIndicateursAnalyses = indicateursUtiles.Count,
                NbValeursValidees = nbValeurs,
                IndicateursUtilises = indicateursUtiles.Select(i => i.Code).ToList(),
            }
        );
    }

    /// <summary>
    /// GET /api/ia/contexte
    /// Renvoie exactement ce que l'IA verrait, sans appeler le modèle.
    /// Sert à démontrer en semaine 8 que seules les valeurs validées sont transmises.
    /// </summary>
    [HttpGet("contexte")]
    public async Task<IActionResult> Contexte([FromQuery] int? indicateurId, CancellationToken ct)
    {
        try
        {
            var indicateurs = await _metier.GetIndicateursValidesAsync(indicateurId, ct);
            var utiles = indicateurs.Where(i => i.Valeurs.Count > 0).ToList();
            return Ok(
                new
                {
                    nbIndicateurs = utiles.Count,
                    nbValeursValidees = utiles.Sum(i => i.Valeurs.Count),
                    prompt = PromptBuilder.Construire(utiles, null),
                }
            );
        }
        catch (Exception ex) when (ex is MetierIndisponibleException or HttpRequestException)
        {
            return StatusCode(502, new { message = "Le service métier est injoignable." });
        }
    }
}
