using Microsoft.EntityFrameworkCore;
using MetierService.Models;

namespace MetierService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Indicateur> Indicateurs { get; set; }
    /// <summary>
        /// Expose la fonction PostgreSQL unaccent() à LINQ.
        ///
        /// Elle permet de traduire la recherche en SQL au lieu de la faire en
        /// mémoire : sans elle, chercher « densite » ne trouverait pas
        /// « Densité médicale ». L'extension est créée par
        /// db/05-documents-et-utilisateurs.sql.
        /// </summary>
        [DbFunction("unaccent", IsBuiltIn = false)]
        public static string Unaccent(string texte) => throw new NotSupportedException();

        public DbSet<Utilisateur> Utilisateurs { get; set; } = null!;
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<ValeurIndicateur> ValeursIndicateurs { get; set; }
    public DbSet<Reclamation> Reclamations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Indicateur>(entity =>
        {
            entity.ToTable("indicateurs");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Nom).HasColumnName("nom");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Statut).HasColumnName("statut");
            entity.Property(e => e.Unite).HasColumnName("unite");
            entity.Property(e => e.SourceDeDonner).HasColumnName("source_de_donner");
            entity.Property(e => e.TypeCollecte).HasColumnName("type_collecte");
            entity.Property(e => e.Frequence).HasColumnName("frequence");
            entity.Property(e => e.ValeurCible).HasColumnName("valeur_cible");
            entity.Property(e => e.AnneeReference).HasColumnName("annee_reference");
            entity.Property(e => e.CategorieId).HasColumnName("categorie_id");
        });

        modelBuilder.Entity<ValeurIndicateur>(entity =>
        {
            entity.ToTable("valeurs_indicateurs");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IndicateurId).HasColumnName("indicateur_id");
            entity.Property(e => e.OrganisationId).HasColumnName("organisation_id");
            entity.Property(e => e.PeriodeId).HasColumnName("periode_id");
            entity.Property(e => e.Valeur).HasColumnName("valeur");
            entity.Property(e => e.Pays).HasColumnName("pays");
            entity.Property(e => e.Gouvernorat).HasColumnName("gouvernorat");
            entity.Property(e => e.Statut).HasColumnName("statut");
            entity.Property(e => e.DegreDeFiabilite).HasColumnName("degre_de_fiabilite");
            entity.Property(e => e.SaisiePar).HasColumnName("saisie_par");
            entity.Property(e => e.Commentaire).HasColumnName("commentaire");
            entity.Property(e => e.SaisieLe).HasColumnName("saisie_le");
            entity.Property(e => e.UpdateAt).HasColumnName("update_at");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
            entity.Property(e => e.ValidePar).HasColumnName("valide_par");
        });

        modelBuilder.Entity<Reclamation>(entity =>
        {
            entity.ToTable("reclamations");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.IndicateurId).HasColumnName("indicateur_id");
            entity.Property(e => e.Objet).HasColumnName("objet");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.SoumisPar).HasColumnName("soumis_par");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Statut).HasColumnName("statut");
            entity.Property(e => e.Reponse).HasColumnName("reponse");
            entity.Property(e => e.CreeLe).HasColumnName("cree_le");
            entity.Property(e => e.TraiteLe).HasColumnName("traite_le");

            modelBuilder.Entity<Utilisateur>(entity =>
            {
                entity.ToTable("utilisateurs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.NomUtilisateur).HasColumnName("nom_utilisateur");
                entity.Property(e => e.Role).HasColumnName("role");
                entity.Property(e => e.Actif).HasColumnName("actif");
            });

            modelBuilder.Entity<Document>(entity =>
            {
                // « meta_data » vient du schéma initial : le nom de table reste,
                // seul son usage est désormais défini.
                entity.ToTable("meta_data");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.IndicateurId).HasColumnName("indicateur_id");
                entity.Property(e => e.Description).HasColumnName("description");
                entity.Property(e => e.SourceDonnee).HasColumnName("source_donnee");
                entity.Property(e => e.NomFichier).HasColumnName("nom_fichier");
                entity.Property(e => e.NomStocke).HasColumnName("nom_stocke");
                entity.Property(e => e.TypeMime).HasColumnName("type_mime");
                entity.Property(e => e.TailleOctets).HasColumnName("taille_octets");
                entity.Property(e => e.DeposePar).HasColumnName("depose_par");
                entity.Property(e => e.DeposeLe).HasColumnName("depose_le");
            });
        });
    }
}
