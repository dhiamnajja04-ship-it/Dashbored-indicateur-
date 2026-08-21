using MetierService.Models;
using Xunit;

namespace MetierService.Tests;

/// <summary>
/// Tests du workflow de validation.
///
/// C'est la règle qui décide, in fine, de ce que voit le service IA :
/// seul l'état « Valide » met is_valid = true. Une régression ici enverrait
/// des valeurs non validées au modèle — exactement ce que le sujet interdit.
/// </summary>
public class StatutValeurTests
{
    [Theory]
    [InlineData(StatutValeur.Brouillon, StatutValeur.EnRevue)]
    [InlineData(StatutValeur.Brouillon, StatutValeur.Valide)]
    [InlineData(StatutValeur.EnRevue, StatutValeur.Valide)]
    [InlineData(StatutValeur.EnRevue, StatutValeur.Rejete)]
    [InlineData(StatutValeur.EnRevue, StatutValeur.Brouillon)]
    [InlineData(StatutValeur.Valide, StatutValeur.Brouillon)]
    [InlineData(StatutValeur.Rejete, StatutValeur.Brouillon)]
    public void Transitions_prevues_sont_autorisees(string depuis, string vers)
    {
        Assert.True(StatutValeur.TransitionAutorisee(depuis, vers));
    }

    [Theory]
    [InlineData(StatutValeur.Brouillon, StatutValeur.Rejete)]
    [InlineData(StatutValeur.Valide, StatutValeur.Rejete)]
    [InlineData(StatutValeur.Rejete, StatutValeur.Valide)]
    public void Transitions_non_prevues_sont_refusees(string depuis, string vers)
    {
        Assert.False(StatutValeur.TransitionAutorisee(depuis, vers));
    }

    [Fact]
    public void Rejeter_directement_un_brouillon_est_refuse()
    {
        // On ne rejette que ce qui a été soumis : rejeter un brouillon
        // sauterait l'étape de revue et fausserait la traçabilité.
        Assert.False(StatutValeur.TransitionAutorisee(StatutValeur.Brouillon, StatutValeur.Rejete));
    }

    [Fact]
    public void Une_valeur_validee_ne_peut_pas_etre_rejetee_directement()
    {
        // Il faut d'abord la dévalider : la sortir du périmètre de l'IA est un
        // acte distinct de son rejet.
        Assert.False(StatutValeur.TransitionAutorisee(StatutValeur.Valide, StatutValeur.Rejete));
    }

    [Fact]
    public void Une_transition_vers_le_meme_statut_est_idempotente()
    {
        // Rejouer une validation ne doit pas produire d'erreur : le client peut
        // renvoyer la même requête sans conséquence.
        foreach (var statut in StatutValeur.Tous)
        {
            Assert.True(StatutValeur.TransitionAutorisee(statut, statut));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("StatutInconnu")]
    public void Un_statut_absent_ou_inconnu_est_traite_comme_un_brouillon(string? statut)
    {
        // Données historiques : la reprise de l'existant ne doit pas être bloquée.
        Assert.True(StatutValeur.TransitionAutorisee(statut, StatutValeur.EnRevue));
        Assert.False(StatutValeur.TransitionAutorisee(statut, StatutValeur.Rejete));
    }

    [Theory]
    [InlineData("valide", StatutValeur.Valide)]
    [InlineData("VALIDE", StatutValeur.Valide)]
    [InlineData("enrevue", StatutValeur.EnRevue)]
    public void La_casse_est_normalisee(string saisi, string attendu)
    {
        // Sans normalisation, « valide » et « Valide » créeraient deux états
        // distincts en base et la contrainte CHECK sauterait.
        Assert.True(StatutValeur.EstConnu(saisi));
        Assert.Equal(attendu, StatutValeur.Normaliser(saisi));
    }

    [Fact]
    public void Les_transitions_possibles_sont_annoncees()
    {
        // L'API renvoie cette liste dans son message d'erreur 400 : elle doit
        // rester exacte, sinon le message induit l'utilisateur en erreur.
        var depuisBrouillon = StatutValeur.TransitionsPossibles(StatutValeur.Brouillon);
        Assert.Contains(StatutValeur.EnRevue, depuisBrouillon);
        Assert.Contains(StatutValeur.Valide, depuisBrouillon);
        Assert.DoesNotContain(StatutValeur.Rejete, depuisBrouillon);
    }
}
