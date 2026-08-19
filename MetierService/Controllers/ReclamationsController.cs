using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MetierService.Data;
using System.ComponentModel.DataAnnotations;
using MetierService.Models;

namespace MetierService.Controllers;

/// <summary>
/// Gestion des réclamations déposées sur les indicateurs.
///
/// Règle d'isolement : une réclamation ne touche jamais à une valeur
/// d'indicateur. Elle n'a donc aucun effet sur <c>is_valid</c> et n'entre
/// jamais dans le périmètre transmis au service IA.
/// </summary>
[Route("api/reclamations")]
[ApiController]
public class ReclamationsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReclamationsController> _logger;

    public ReclamationsController(AppDbContext context, ILogger<ReclamationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/reclamations?statut=Nouvelle&indicateurId=1
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Reclamation>>> GetReclamations(
        [FromQuery] string? statut,
        [FromQuery] int? indicateurId)
    {
        var requete = _context.Reclamations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(statut))
        {
            if (!StatutReclamation.EstConnu(statut))
            {
                return BadRequest(new
                {
                    message = $"Statut « {statut} » inconnu.",
                    statutsAcceptes = StatutReclamation.Tous
                });
            }
            var recherche = StatutReclamation.Normaliser(statut);
            requete = requete.Where(r => r.Statut == recherche);
        }

        if (indicateurId.HasValue)
            requete = requete.Where(r => r.IndicateurId == indicateurId.Value);

        // Les plus récentes d'abord : c'est l'ordre utile pour un suivi.
        return await requete.OrderByDescending(r => r.CreeLe).ToListAsync();
    }

    // GET: api/reclamations/5
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Reclamation>> GetReclamation(int id)
    {
        var reclamation = await _context.Reclamations.FindAsync(id);
        if (reclamation == null) return NotFound(new { message = "Réclamation introuvable." });
        return reclamation;
    }

    // GET: api/reclamations/statistiques
    [HttpGet("statistiques")]
    public async Task<IActionResult> Statistiques()
    {
        var parStatut = await _context.Reclamations
            .GroupBy(r => r.Statut)
            .Select(g => new { statut = g.Key, nombre = g.Count() })
            .ToListAsync();

        return Ok(new
        {
            total = parStatut.Sum(x => x.nombre),
            parStatut,
        });
    }

    // POST: api/reclamations
    [HttpPost]
    public async Task<ActionResult<Reclamation>> PostReclamation(Reclamation reclamation)
    {
        // Champ facultatif : une chaîne vide vaut « non renseigné ».
        reclamation.Email = string.IsNullOrWhiteSpace(reclamation.Email)
            ? null
            : reclamation.Email.Trim();

        if (reclamation.Email is not null && !new EmailAddressAttribute().IsValid(reclamation.Email))
            return BadRequest(new { message = "L'adresse électronique n'est pas valide." });

        if (reclamation.IndicateurId.HasValue)
        {
            var existe = await _context.Indicateurs.AnyAsync(i => i.Id == reclamation.IndicateurId.Value);
            if (!existe)
                return BadRequest(new { message = "L'indicateur visé par la réclamation n'existe pas." });
        }

        // Le client ne choisit ni le statut ni les dates : une réclamation
        // entre toujours dans le circuit par l'état « Nouvelle ».
        reclamation.Statut = StatutReclamation.Nouvelle;
        reclamation.Reponse = null;
        reclamation.TraiteLe = null;
        reclamation.CreeLe = DateTime.UtcNow;

        _context.Reclamations.Add(reclamation);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'enregistrement d'une réclamation");
            return StatusCode(500, new { message = "Erreur interne lors de l'enregistrement de la réclamation." });
        }

        _logger.LogInformation(
            "Réclamation {Id} déposée par {Auteur} sur l'indicateur {IndicateurId}",
            reclamation.Id, reclamation.SoumisPar, reclamation.IndicateurId);

        return CreatedAtAction(nameof(GetReclamation), new { id = reclamation.Id }, reclamation);
    }

    // PATCH: api/reclamations/5/statut
    [HttpPatch("{id:int}/statut")]
    public async Task<IActionResult> ChangerStatut(int id, [FromBody] ChangementStatutReclamation requete)
    {
        if (requete == null || string.IsNullOrWhiteSpace(requete.Statut))
            return BadRequest(new { message = "Le statut cible est obligatoire." });

        if (!StatutReclamation.EstConnu(requete.Statut))
            return BadRequest(new
            {
                message = $"Statut « {requete.Statut} » inconnu.",
                statutsAcceptes = StatutReclamation.Tous
            });

        var reclamation = await _context.Reclamations.FindAsync(id);
        if (reclamation == null) return NotFound(new { message = "Réclamation introuvable." });

        var cible = StatutReclamation.Normaliser(requete.Statut);
        if (!StatutReclamation.TransitionAutorisee(reclamation.Statut, cible))
        {
            return BadRequest(new
            {
                message = $"Transition « {reclamation.Statut} » → « {cible} » non autorisée.",
                statutActuel = reclamation.Statut,
                transitionsPossibles = StatutReclamation.TransitionsPossibles(reclamation.Statut)
            });
        }

        reclamation.Statut = cible;
        if (!string.IsNullOrWhiteSpace(requete.Reponse))
            reclamation.Reponse = requete.Reponse;

        // Traitée ou rejetée : le dossier est clos, on horodate.
        reclamation.TraiteLe =
            cible is StatutReclamation.Traitee or StatutReclamation.Rejetee
                ? DateTime.UtcNow
                : null;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du changement de statut de la réclamation {Id}", id);
            return StatusCode(500, new { message = "Erreur interne lors du changement de statut." });
        }

        return Ok(new
        {
            id = reclamation.Id,
            statut = reclamation.Statut,
            reponse = reclamation.Reponse,
            traiteLe = reclamation.TraiteLe,
            transitionsPossibles = StatutReclamation.TransitionsPossibles(reclamation.Statut)
        });
    }

    // DELETE: api/reclamations/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteReclamation(int id)
    {
        var reclamation = await _context.Reclamations.FindAsync(id);
        if (reclamation == null) return NotFound(new { message = "Réclamation introuvable." });

        _context.Reclamations.Remove(reclamation);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression de la réclamation {Id}", id);
            return StatusCode(500, new { message = "Erreur interne lors de la suppression." });
        }
        return NoContent();
    }
}
