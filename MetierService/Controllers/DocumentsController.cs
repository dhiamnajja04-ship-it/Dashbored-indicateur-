using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MetierService.Data;
using MetierService.Models;

namespace MetierService.Controllers;

/// <summary>
/// Documents justificatifs attachés à un indicateur.
///
/// Le fichier est écrit sur un volume monté ; la base ne garde que ses
/// métadonnées. Deux règles de sûreté guident ce contrôleur :
///   - le nom d'origine n'est JAMAIS utilisé comme nom de fichier sur le
///     disque (un nom comme « ../../etc/passwd » sortirait du dossier) ;
///   - seules des extensions documentaires sont acceptées, pour qu'un fichier
///     déposé ne puisse pas être servi comme du code exécutable.
/// </summary>
[Route("api/indicators")]
[ApiController]
public class DocumentsController : ControllerBase
{
    private const long TailleMax = 10 * 1024 * 1024; // 10 Mo

    private static readonly HashSet<string> ExtensionsAutorisees = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".csv", ".xlsx", ".xls", ".ods", ".doc", ".docx", ".txt", ".png", ".jpg", ".jpeg",
    };

    private readonly AppDbContext _context;
    private readonly ILogger<DocumentsController> _logger;
    private readonly string _dossier;

    public DocumentsController(AppDbContext context, IConfiguration config, ILogger<DocumentsController> logger)
    {
        _context = context;
        _logger = logger;
        _dossier = config["Stockage:Documents"] ?? "/donnees/documents";
        Directory.CreateDirectory(_dossier);
    }

    /// <summary>Documents attachés à un indicateur.</summary>
    [HttpGet("{id:int}/documents")]
    public async Task<IActionResult> Lister(int id)
    {
        var documents = await _context.Documents
            .Where(d => d.IndicateurId == id && d.NomStocke != null)
            .OrderByDescending(d => d.DeposeLe)
            .ToListAsync();

        return Ok(documents);
    }

    /// <summary>Dépose un fichier et enregistre ses métadonnées.</summary>
    [HttpPost("{id:int}/documents")]
    [RequestSizeLimit(TailleMax + 4096)]
    public async Task<IActionResult> Deposer(int id, IFormFile fichier, [FromForm] string? description, [FromForm] string? deposePar)
    {
        if (!await _context.Indicateurs.AnyAsync(i => i.Id == id))
        {
            return NotFound(new { message = $"Aucun indicateur d'identifiant {id}." });
        }

        if (fichier is null || fichier.Length == 0)
        {
            return BadRequest(new { message = "Aucun fichier reçu." });
        }

        if (fichier.Length > TailleMax)
        {
            return BadRequest(new { message = $"Fichier trop volumineux : {TailleMax / 1024 / 1024} Mo maximum." });
        }

        // Path.GetFileName neutralise un chemin glissé dans le nom d'origine.
        var nomOrigine = Path.GetFileName(fichier.FileName);
        var extension = Path.GetExtension(nomOrigine);

        if (string.IsNullOrWhiteSpace(extension) || !ExtensionsAutorisees.Contains(extension))
        {
            return BadRequest(new
            {
                message = $"Extension non autorisée. Formats acceptés : {string.Join(", ", ExtensionsAutorisees)}.",
            });
        }

        // Le nom sur le disque est généré : il ne dépend pas de l'entrée utilisateur.
        var nomStocke = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var chemin = Path.Combine(_dossier, nomStocke);

        await using (var flux = System.IO.File.Create(chemin))
        {
            await fichier.CopyToAsync(flux);
        }

        var document = new Document
        {
            IndicateurId = id,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            NomFichier = nomOrigine,
            NomStocke = nomStocke,
            TypeMime = fichier.ContentType,
            TailleOctets = fichier.Length,
            DeposePar = string.IsNullOrWhiteSpace(deposePar) ? null : deposePar.Trim(),
            DeposeLe = DateTime.UtcNow,
        };

        _context.Documents.Add(document);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Document {Nom} ({Taille} octets) attaché à l'indicateur {Id}",
            nomOrigine, fichier.Length, id);

        return CreatedAtAction(nameof(Lister), new { id }, document);
    }

    /// <summary>Télécharge un document.</summary>
    [HttpGet("documents/{documentId:int}")]
    public async Task<IActionResult> Telecharger(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document?.NomStocke is null)
        {
            return NotFound(new { message = "Document introuvable." });
        }

        var chemin = Path.Combine(_dossier, document.NomStocke);
        if (!System.IO.File.Exists(chemin))
        {
            _logger.LogError("Fichier absent du volume : {Chemin}", chemin);
            return NotFound(new { message = "Le fichier n'est plus présent sur le serveur." });
        }

        var flux = System.IO.File.OpenRead(chemin);
        return File(flux, document.TypeMime ?? "application/octet-stream", document.NomFichier);
    }

    /// <summary>Supprime un document et son fichier.</summary>
    [HttpDelete("documents/{documentId:int}")]
    public async Task<IActionResult> Supprimer(int documentId)
    {
        var document = await _context.Documents.FindAsync(documentId);
        if (document is null)
        {
            return NotFound(new { message = "Document introuvable." });
        }

        if (!string.IsNullOrWhiteSpace(document.NomStocke))
        {
            var chemin = Path.Combine(_dossier, document.NomStocke);
            // Le fichier peut déjà avoir disparu : ce n'est pas une raison de
            // laisser la ligne en base.
            if (System.IO.File.Exists(chemin)) System.IO.File.Delete(chemin);
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

/// <summary>Référentiel des agents (table « utilisateurs »).</summary>
[Route("api/utilisateurs")]
[ApiController]
public class UtilisateursController : ControllerBase
{
    private readonly AppDbContext _context;

    public UtilisateursController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Lister([FromQuery] string? role)
    {
        var requete = _context.Utilisateurs.Where(u => u.Actif);

        if (!string.IsNullOrWhiteSpace(role))
        {
            requete = requete.Where(u => u.Role == role);
        }

        return Ok(await requete.OrderBy(u => u.NomUtilisateur).ToListAsync());
    }
}
