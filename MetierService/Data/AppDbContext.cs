using Microsoft.EntityFrameworkCore;
using MetierService.Models;

namespace MetierService.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Indicateur> Indicateurs { get; set; }
    public DbSet<ValeurIndicateur> ValeursIndicateurs { get; set; }

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
            entity.Property(e => e.Statut).HasColumnName("statut");
            entity.Property(e => e.DegreDeFiabilite).HasColumnName("degre_de_fiabilite");
            entity.Property(e => e.SaisiePar).HasColumnName("saisie_par");
            entity.Property(e => e.Commentaire).HasColumnName("commentaire");
            entity.Property(e => e.SaisieLe).HasColumnName("saisie_le");
            entity.Property(e => e.UpdateAt).HasColumnName("update_at");
            entity.Property(e => e.IsValid).HasColumnName("is_valid");
            entity.Property(e => e.ValidePar).HasColumnName("valide_par");
        });
    }
}
