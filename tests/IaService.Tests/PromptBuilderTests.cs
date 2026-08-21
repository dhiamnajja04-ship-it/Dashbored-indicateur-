using IaService.Models;
using IaService.Services;
using Xunit;

namespace IaService.Tests;

/// <summary>
/// Tests de construction du prompt.
///
/// Le prompt EST le périmètre : tout ce que le modèle « sait » vient de là.
/// Ces tests vérifient donc ce qui y entre, ce qui n'y entre pas, et que les
/// calculs faits pour le modèle sont justes — un petit modèle recopie, il ne
/// vérifie pas.
/// </summary>
public class PromptBuilderTests
{
    private static IndicateurValide Indicateur(
        string code = "IND-TEST",
        string nom = "Indicateur de test",
        string unite = "%",
        decimal? cible = null,
        params ValeurValide[] valeurs) => new()
    {
        Code = code,
        Nom = nom,
        Unite = unite,
        ValeurCible = cible,
        Valeurs = valeurs.ToList(),
    };

    private static ValeurValide Valeur(
        decimal montant,
        string? pays = "Tunisie",
        string? gouvernorat = null,
        string? organisation = null,
        string? periode = null) => new()
    {
        Valeur = montant,
        Pays = pays,
        Gouvernorat = gouvernorat,
        OrganisationNom = organisation,
        PeriodeLibelle = periode,
        SaisieLe = new DateTime(2025, 6, 15),
    };

    [Fact]
    public void Le_prompt_contient_les_indicateurs_fournis()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(code: "IND-CHOM", nom: "Taux de chômage", valeurs: Valeur(12.4m)) },
            null);

        Assert.Contains("IND-CHOM", prompt);
        Assert.Contains("Taux de chômage", prompt);
        Assert.Contains("12,4", prompt);
    }

    [Fact]
    public void Le_prompt_ne_contient_que_ce_qui_lui_est_transmis()
    {
        // Le filtrage est fait par le service métier ; ce test garantit que le
        // PromptBuilder n'ajoute rien de son cru.
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(code: "IND-CHOM", valeurs: Valeur(12.4m)) },
            null);

        Assert.DoesNotContain("IND-SCOL", prompt);
        Assert.DoesNotContain("IND-INFL", prompt);
    }

    [Fact]
    public void Sans_indicateur_le_prompt_reste_construit_et_vide_de_donnees()
    {
        var prompt = PromptBuilder.Construire(Array.Empty<IndicateurValide>(), null);

        Assert.Contains("=== DONNÉES VALIDÉES ===", prompt);
        Assert.Contains("=== FIN DES DONNÉES ===", prompt);
    }

    [Theory]
    [InlineData(12.4, 10, "AU-DESSUS")]
    [InlineData(96.2, 98, "EN DESSOUS")]
    [InlineData(50, 50, "ÉGAL")]
    public void L_ecart_a_la_cible_est_calcule_et_son_sens_est_juste(
        double valeur, double cible, string sensAttendu)
    {
        // Le modèle se trompait en comparant deux nombres (« 12,4 est en dessous
        // de 10 »). Le calcul est donc fait ici et fourni tout fait.
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(cible: (decimal)cible, valeurs: Valeur((decimal)valeur)) },
            null);

        Assert.Contains("Position :", prompt);
        Assert.Contains(sensAttendu, prompt);
    }

    [Fact]
    public void L_ecart_affiche_est_la_difference_absolue()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(cible: 10m, valeurs: Valeur(12.4m)) },
            null);

        Assert.Contains("2,4", prompt);   // 12,4 - 10
        Assert.DoesNotContain("-2,4", prompt);
    }

    [Fact]
    public void Sans_cible_aucune_position_n_est_affirmee()
    {
        // Afficher « +0 » laisserait croire que la cible est atteinte alors
        // qu'il n'y en a pas.
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(cible: null, valeurs: Valeur(42m)) },
            null);

        Assert.DoesNotContain("Position :", prompt);
    }

    [Fact]
    public void Une_valeur_nationale_et_une_valeur_regionale_sont_distinguees()
    {
        var national = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m, gouvernorat: null)) }, null);
        var regional = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(18.7m, gouvernorat: "Kasserine")) }, null);

        Assert.Contains("niveau national", national);
        Assert.Contains("Kasserine", regional);
        Assert.DoesNotContain("niveau national", regional);
    }

    [Fact]
    public void Les_libelles_remplacent_les_identifiants_quand_ils_existent()
    {
        // Le modèle recevait « organisation #1 » et le recrachait tel quel.
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m, organisation: "Ministère du Plan", periode: "Année 2025")) },
            null);

        Assert.Contains("Ministère du Plan", prompt);
        Assert.Contains("Année 2025", prompt);
        Assert.DoesNotContain("organisation #", prompt);
    }

    [Fact]
    public void Sans_libelle_l_identifiant_sert_de_repli()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m, organisation: null, periode: null)) },
            null);

        Assert.Contains("organisation #", prompt);
    }

    [Fact]
    public void Une_seule_mesure_interdit_explicitement_de_parler_de_tendance()
    {
        // Demander « les tendances » sur une valeur unique poussait le modèle
        // à en inventer une.
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m)) }, null);

        Assert.Contains("ne parle NI de tendance", prompt);
    }

    [Fact]
    public void Plusieurs_mesures_autorisent_l_evolution()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: new[] { Valeur(12.4m), Valeur(11.8m) }) }, null);

        Assert.Contains("plusieurs valeurs", prompt);
        Assert.DoesNotContain("ne parle NI de tendance", prompt);
    }

    [Fact]
    public void La_question_de_l_utilisateur_remplace_la_synthese_generale()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m)) },
            "Quels indicateurs dépassent leur cible ?");

        Assert.Contains("QUESTION DE L'UTILISATEUR", prompt);
        Assert.Contains("Quels indicateurs dépassent leur cible ?", prompt);
    }

    [Fact]
    public void Les_regles_anti_invention_sont_toujours_presentes()
    {
        var prompt = PromptBuilder.Construire(
            new[] { Indicateur(valeurs: Valeur(12.4m)) }, null);

        Assert.Contains("N'invente aucun chiffre", prompt);
        Assert.Contains("Utilise UNIQUEMENT les données", prompt);
    }
}
