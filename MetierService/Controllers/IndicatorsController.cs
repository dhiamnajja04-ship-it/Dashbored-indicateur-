using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MetierService.Data;
using MetierService.Models;
using Npgsql;

namespace MetierService.Controllers
{
    [Route("api/indicators")]
    [ApiController]
    public class IndicatorsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<IndicatorsController> _logger;

        public IndicatorsController(AppDbContext context, ILogger<IndicatorsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/indicators
        /// <summary>
        /// Liste des indicateurs.
        ///
        /// Sans paramètre, renvoie tout : les clients existants ne sont pas
        /// cassés. Avec « page », renvoie un objet paginé — c'est un choix
        /// délibéré plutôt qu'une pagination imposée, pour que le service IA et
        /// les scripts curl continuent de fonctionner à l'identique.
        ///
        /// « recherche » filtre côté base : jusqu'ici le tri et le filtrage
        /// étaient faits dans le navigateur, ce qui supposait de charger la
        /// table entière.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetIndicateurs(
            [FromQuery] int? page,
            [FromQuery] int taille = 10,
            [FromQuery] string? recherche = null,
            [FromQuery] string? tri = null,
            [FromQuery] bool desc = false)
        {
            var requete = _context.Indicateurs.Include(i => i.ValeursIndicateurs).AsQueryable();

            if (!string.IsNullOrWhiteSpace(recherche))
            {
                // Recherche insensible à la casse ET aux accents : sur un corpus
                // francophone, « densite » doit trouver « Densité médicale ».
                // Le terme est normalisé ICI, en .NET : AppDbContext.Unaccent
                // n'existe que pour être traduite en SQL, l'appeler côté client
                // lèverait NotSupportedException. Seules les colonnes la traversent.
                var terme = SansAccents(recherche.Trim().ToLower());
                requete = requete.Where(i =>
                    AppDbContext.Unaccent(i.Code.ToLower()).Contains(terme)
                    || AppDbContext.Unaccent(i.Nom.ToLower()).Contains(terme)
                    || (i.Description != null && AppDbContext.Unaccent(i.Description.ToLower()).Contains(terme))
                    || (i.Unite != null && AppDbContext.Unaccent(i.Unite.ToLower()).Contains(terme))
                    || (i.SourceDeDonner != null && AppDbContext.Unaccent(i.SourceDeDonner.ToLower()).Contains(terme))
                    || (i.Frequence != null && AppDbContext.Unaccent(i.Frequence.ToLower()).Contains(terme)));
            }

            requete = (tri, desc) switch
            {
                ("nom", false) => requete.OrderBy(i => i.Nom),
                ("nom", true) => requete.OrderByDescending(i => i.Nom),
                ("unite", false) => requete.OrderBy(i => i.Unite),
                ("unite", true) => requete.OrderByDescending(i => i.Unite),
                ("source", false) => requete.OrderBy(i => i.SourceDeDonner),
                ("source", true) => requete.OrderByDescending(i => i.SourceDeDonner),
                (_, true) => requete.OrderByDescending(i => i.Code),
                _ => requete.OrderBy(i => i.Code),
            };

            if (page is null)
            {
                return Ok(await requete.ToListAsync());
            }

            var numero = Math.Max(1, page.Value);
            var parPage = Math.Clamp(taille, 1, 100);
            var total = await requete.CountAsync();

            var elements = await requete
                .Skip((numero - 1) * parPage)
                .Take(parPage)
                .ToListAsync();

            return Ok(new PageResultat<Indicateur>
            {
                Elements = elements,
                Page = numero,
                Taille = parPage,
                Total = total,
            });
        }

        // GET: api/indicators/validated
        // Consommé par le service IA. Ne renvoie QUE les valeurs is_valid = true,
        // et seulement les indicateurs qui en possèdent au moins une.
        // C'est ici qu'est appliquée la règle métier de la semaine 8 : le filtrage
        // est fait à la source, l'IA n'a jamais accès aux valeurs non validées.
        [HttpGet("validated")]
        public async Task<ActionResult<IEnumerable<IndicateurValideDto>>> GetIndicateursValides(
            [FromQuery] int? indicateurId)
        {
            var requete = _context.Indicateurs.AsNoTracking().AsQueryable();

            if (indicateurId.HasValue)
                requete = requete.Where(i => i.Id == indicateurId.Value);

            // Filtrer AVANT la projection : EF traduit ce Where en EXISTS côté SQL.
            // Placé après le Select, il porterait sur une liste déjà matérialisée
            // et ne serait pas traduisible.
            requete = requete.Where(i => i.ValeursIndicateurs.Any(v => v.IsValid));

            var resultat = await requete
                .Select(i => new IndicateurValideDto
                {
                    Id = i.Id,
                    Code = i.Code,
                    Nom = i.Nom,
                    Description = i.Description,
                    Unite = i.Unite,
                    Frequence = i.Frequence,
                    ValeurCible = i.ValeurCible,
                    AnneeReference = i.AnneeReference,
                    Valeurs = i.ValeursIndicateurs
                        .Where(v => v.IsValid)
                        .Select(v => new ValeurValideDto
                        {
                            Id = v.Id,
                            Valeur = v.Valeur,
                            Pays = v.Pays,
                            Gouvernorat = v.Gouvernorat,
                            Statut = v.Statut,
                            DegreDeFiabilite = v.DegreDeFiabilite,
                            Commentaire = v.Commentaire,
                            SaisieLe = v.SaisieLe,
                            ValidePar = v.ValidePar,
                            OrganisationId = v.OrganisationId,
                            PeriodeId = v.PeriodeId,
                        })
                        .ToList(),
                })
                .ToListAsync();

            _logger.LogInformation(
                "Contexte IA : {NbIndicateurs} indicateur(s) validé(s), {NbValeurs} valeur(s)",
                resultat.Count,
                resultat.Sum(i => i.Valeurs.Count));

            return resultat;
        }

        // GET: api/indicators/5
        [HttpGet("{id:int}")]
        public async Task<ActionResult<Indicateur>> GetIndicateur(int id)
        {
            var indicateur = await _context.Indicateurs
                .Include(i => i.ValeursIndicateurs)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (indicateur == null) return NotFound(new { message = "Indicateur introuvable." });
            return indicateur;
        }

        // POST: api/indicators
        [HttpPost]
        public async Task<ActionResult<Indicateur>> PostIndicateur(Indicateur indicateur)
        {
            _context.Indicateurs.Add(indicateur);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDbUpdateException(ex, $"le code « {indicateur.Code} »");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la création d'un indicateur");
                return StatusCode(500, new { message = "Erreur interne lors de la création de l'indicateur." });
            }
            return CreatedAtAction(nameof(GetIndicateur), new { id = indicateur.Id }, indicateur);
        }

        // PUT: api/indicators/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> PutIndicateur(int id, Indicateur indicateur)
        {
            if (id != indicateur.Id)
                return BadRequest(new { message = "L'identifiant de l'URL ne correspond pas à celui de l'indicateur." });

            _context.Entry(indicateur).State = EntityState.Modified;
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                var exists = await _context.Indicateurs.AnyAsync(i => i.Id == id);
                if (!exists) return NotFound(new { message = "Cet indicateur n'existe plus." });
                throw;
            }
            catch (DbUpdateException ex)
            {
                return HandleDbUpdateException(ex, $"le code « {indicateur.Code} »");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la mise à jour d'un indicateur");
                return StatusCode(500, new { message = "Erreur interne lors de la mise à jour de l'indicateur." });
            }
            return NoContent();
        }

        // DELETE: api/indicators/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteIndicateur(int id)
        {
            var indicateur = await _context.Indicateurs.FindAsync(id);
            if (indicateur == null) return NotFound(new { message = "Indicateur introuvable." });

            _context.Indicateurs.Remove(indicateur);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la suppression d'un indicateur");
                return StatusCode(500, new { message = "Erreur interne lors de la suppression de l'indicateur." });
            }
            return NoContent();
        }

        // --- GESTION DES VALEURS (Page Consulter / Saisie / Validation Admin) ---

        // GET: api/indicators/5/valeurs
        // ?validesUniquement=true permet au frontend d'afficher la même vue que l'IA.
        [HttpGet("{id:int}/valeurs")]
        public async Task<ActionResult<IEnumerable<ValeurIndicateur>>> GetValeurs(
            int id,
            [FromQuery] bool validesUniquement = false)
        {
            var requete = _context.ValeursIndicateurs.Where(v => v.IndicateurId == id);
            if (validesUniquement)
                requete = requete.Where(v => v.IsValid);

            return await requete.ToListAsync();
        }

        // POST: api/indicators/5/valeurs (Saisie par un utilisateur)
        [HttpPost("{id:int}/valeurs")]
        public async Task<ActionResult<ValeurIndicateur>> PostValeur(int id, ValeurIndicateur valeur)
        {
            var indicateurExiste = await _context.Indicateurs.AnyAsync(i => i.Id == id);
            if (!indicateurExiste) return NotFound(new { message = "Indicateur introuvable." });

            valeur.IndicateurId = id;
            valeur.SaisieLe = DateTime.UtcNow;
            valeur.UpdateAt = DateTime.UtcNow;

            // Entrée du workflow : une valeur saisie est toujours un brouillon.
            // Le client ne peut pas s'auto-valider en postant is_valid = true.
            valeur.IsValid = false;
            valeur.Statut = StatutValeur.Brouillon;
            valeur.ValidePar = string.Empty;

            _context.ValeursIndicateurs.Add(valeur);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDbUpdateException(ex, "cette valeur");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la création d'une valeur");
                return StatusCode(500, new { message = "Erreur interne lors de la création de la valeur." });
            }
            return Ok(valeur);
        }

        // PUT: api/indicators/values/5 (Modifier une valeur)
        [HttpPut("values/{valueId:int}")]
        public async Task<IActionResult> PutValeur(int valueId, ValeurIndicateur valeur)
        {
            if (valueId != valeur.Id)
                return BadRequest(new { message = "L'identifiant de l'URL ne correspond pas à celui de la valeur." });

            var existante = await _context.ValeursIndicateurs.FindAsync(valueId);
            if (existante == null) return NotFound(new { message = "Cette valeur n'existe plus." });

            // On recopie uniquement les champs modifiables. Le statut de validation
            // n'est PAS pris depuis le corps de la requête : sans cela, un client
            // pourrait poster is_valid = true et court-circuiter le workflow, donc
            // faire entrer dans l'IA une valeur jamais relue par un validateur.
            var chiffreModifie = existante.Valeur != valeur.Valeur;

            existante.Valeur = valeur.Valeur;
            existante.Pays = valeur.Pays;
            existante.Gouvernorat = valeur.Gouvernorat;
            existante.OrganisationId = valeur.OrganisationId;
            existante.PeriodeId = valeur.PeriodeId;
            existante.DegreDeFiabilite = valeur.DegreDeFiabilite;
            existante.Commentaire = valeur.Commentaire;
            existante.SaisiePar = valeur.SaisiePar;
            existante.UpdateAt = DateTime.UtcNow;

            // Un chiffre déjà validé qui change doit repasser par la validation,
            // sinon l'IA analyserait une donnée que personne n'a relue sous sa forme actuelle.
            if (chiffreModifie && existante.IsValid)
            {
                existante.IsValid = false;
                existante.Statut = StatutValeur.Brouillon;
                existante.ValidePar = string.Empty;
                _logger.LogInformation(
                    "Valeur {ValueId} modifiée après validation : retour au statut Brouillon", valueId);
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return HandleDbUpdateException(ex, "cette valeur");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la mise à jour d'une valeur");
                return StatusCode(500, new { message = "Erreur interne lors de la mise à jour de la valeur." });
            }
            return NoContent();
        }

        // --- WORKFLOW DE VALIDATION (semaine 8) ---

        // PATCH: api/indicators/values/5/validate
        // Passe la valeur à l'état Valide : elle devient visible par le service IA.
        [HttpPatch("values/{valueId:int}/validate")]
        public Task<IActionResult> ValiderValeur(int valueId, [FromBody] ValidationRequest? requete)
            => ChangerStatut(valueId, StatutValeur.Valide, requete?.Utilisateur, requete?.Commentaire);

        // PATCH: api/indicators/values/5/devalidate
        // Retire la valeur du périmètre de l'IA sans la supprimer.
        [HttpPatch("values/{valueId:int}/devalidate")]
        public Task<IActionResult> DevaliderValeur(int valueId, [FromBody] ValidationRequest? requete)
            => ChangerStatut(valueId, StatutValeur.Brouillon, requete?.Utilisateur, requete?.Commentaire);

        // PATCH: api/indicators/values/5/statut
        // Transition libre dans le workflow (Brouillon / EnRevue / Valide / Rejete).
        [HttpPatch("values/{valueId:int}/statut")]
        public async Task<IActionResult> ChangerStatutValeur(
            int valueId,
            [FromBody] ChangementStatutRequest requete)
        {
            if (requete == null || string.IsNullOrWhiteSpace(requete.Statut))
                return BadRequest(new { message = "Le statut cible est obligatoire." });

            if (!StatutValeur.EstConnu(requete.Statut))
                return BadRequest(new
                {
                    message = $"Statut « {requete.Statut} » inconnu.",
                    statutsAcceptes = StatutValeur.Tous
                });

            return await ChangerStatut(
                valueId,
                StatutValeur.Normaliser(requete.Statut),
                requete.Utilisateur,
                requete.Commentaire);
        }

        // PUT: api/indicators/values/5/valider
        // Ancienne route (semaines 4 à 7), conservée pour ne pas casser les scripts curl
        // déjà écrits. Le corps attendu est une simple chaîne JSON : "Administrateur".
        [HttpPut("values/{valueId:int}/valider")]
        public Task<IActionResult> ValiderValeurLegacy(int valueId, [FromBody] string? adminNom)
            => ChangerStatut(valueId, StatutValeur.Valide, adminNom, null);

        /// <summary>
        /// Point d'entrée unique du workflow : vérifie que la transition est permise,
        /// puis synchronise le drapeau is_valid — qui est le seul critère lu par l'IA.
        /// </summary>
        private async Task<IActionResult> ChangerStatut(
            int valueId,
            string statutCible,
            string? utilisateur,
            string? commentaire)
        {
            var valeur = await _context.ValeursIndicateurs.FindAsync(valueId);
            if (valeur == null) return NotFound(new { message = "Cette valeur n'existe pas." });

            if (!StatutValeur.TransitionAutorisee(valeur.Statut, statutCible))
            {
                return BadRequest(new
                {
                    message = $"Transition « {valeur.Statut} » → « {statutCible} » non autorisée.",
                    statutActuel = valeur.Statut,
                    transitionsPossibles = StatutValeur.TransitionsPossibles(valeur.Statut)
                });
            }

            var ancienStatut = valeur.Statut;
            valeur.Statut = statutCible;
            valeur.IsValid = statutCible == StatutValeur.Valide;
            valeur.ValidePar = valeur.IsValid ? (utilisateur ?? "Administrateur") : string.Empty;
            valeur.UpdateAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(commentaire))
                valeur.Commentaire = commentaire;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur lors du changement de statut de la valeur {ValueId}", valueId);
                return StatusCode(500, new { message = "Erreur interne lors du changement de statut." });
            }

            _logger.LogInformation(
                "Valeur {ValueId} : « {Ancien} » → « {Nouveau} » par {Utilisateur} (is_valid={IsValid})",
                valueId, ancienStatut, statutCible, utilisateur ?? "Administrateur", valeur.IsValid);

            return Ok(new
            {
                id = valeur.Id,
                statut = valeur.Statut,
                isValid = valeur.IsValid,
                validePar = valeur.ValidePar,
                updateAt = valeur.UpdateAt,
                transitionsPossibles = StatutValeur.TransitionsPossibles(valeur.Statut)
            });
        }

        // DELETE: api/indicators/values/5 (Supprimer une valeur)
        [HttpDelete("values/{valueId:int}")]
        public async Task<IActionResult> DeleteValeur(int valueId)
        {
            var valeur = await _context.ValeursIndicateurs.FindAsync(valueId);
            if (valeur == null) return NotFound(new { message = "Cette valeur n'existe pas." });

            _context.ValeursIndicateurs.Remove(valeur);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur inattendue lors de la suppression d'une valeur");
                return StatusCode(500, new { message = "Erreur interne lors de la suppression de la valeur." });
            }
            return NoContent();
        }

        /// <summary>
        /// Traduit les erreurs PostgreSQL courantes (contrainte unique, clé étrangère, champ
        /// obligatoire manquant) en réponse HTTP lisible par le frontend, au lieu de laisser
        /// remonter un 500 brut avec la stack trace complète.
        /// </summary>
        private ActionResult HandleDbUpdateException(DbUpdateException ex, string contexte)
        {
            _logger.LogError(ex, "Erreur de mise à jour en base de données");

            if (ex.InnerException is PostgresException pgEx)
            {
                switch (pgEx.SqlState)
                {
                    case "23505": // unique_violation
                        return Conflict(new
                        {
                            message = $"Un enregistrement avec {contexte} existe déjà. Merci d'utiliser une valeur différente."
                        });
                    case "23503": // foreign_key_violation
                        return BadRequest(new
                        {
                            message = "Référence invalide : la catégorie, l'organisation ou la période indiquée n'existe pas."
                        });
                    case "23502": // not_null_violation
                        return BadRequest(new
                        {
                            message = $"Un champ obligatoire est manquant ({pgEx.ColumnName})."
                        });
                }
            }

            return StatusCode(500, new { message = "Erreur lors de l'enregistrement en base de données." });
        }

        /// <summary>
        /// Retire les diacritiques d'une chaîne, côté application.
        /// Pendant du unaccent() de PostgreSQL appliqué aux colonnes.
        /// </summary>
        private static string SansAccents(string texte)
        {
            var decompose = texte.Normalize(System.Text.NormalizationForm.FormD);
            var sortie = new System.Text.StringBuilder(decompose.Length);

            foreach (var c in decompose)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                    != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sortie.Append(c);
                }
            }

            return sortie.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }
    }
}
