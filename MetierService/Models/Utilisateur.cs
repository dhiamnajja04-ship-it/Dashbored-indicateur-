namespace MetierService.Models;

/// <summary>
/// Agent du référentiel (table « utilisateurs »).
///
/// Ce n'est PAS un compte : il n'y a ni mot de passe ni connexion. La table
/// sert à proposer une liste fermée là où « saisi par » et « validé par »
/// étaient auparavant du texte libre — deux orthographes du même nom
/// produisaient deux agents distincts dans les statistiques.
/// </summary>
public class Utilisateur
{
    public int Id { get; set; }
    public string NomUtilisateur { get; set; } = string.Empty;
    public string? Role { get; set; }
    public bool Actif { get; set; } = true;
}
