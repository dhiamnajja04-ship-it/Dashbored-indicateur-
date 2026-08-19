namespace MetierService.Models
{
    public class ValeurIndicateur
    {
        public int Id { get; set; }
        public int IndicateurId { get; set; }
        public int OrganisationId { get; set; }
        public int PeriodeId { get; set; }
        public decimal Valeur { get; set; }
        public string Statut { get; set; } = string.Empty;
        public string DegreDeFiabilite { get; set; } = string.Empty;
        public string Commentaire { get; set; } = string.Empty;
        public DateTime SaisieLe { get; set; } = DateTime.UtcNow;
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;
        public bool IsValid { get; set; } = false;
        public string SaisiePar { get; set; } = string.Empty;
        public string ValidePar { get; set; } = string.Empty;
    }
}
