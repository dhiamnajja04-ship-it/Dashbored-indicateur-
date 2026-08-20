import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RoleService, Role, ROLES } from '../../services/role.service';
import { IndicateurService, Utilisateur } from '../../services/indicateur.service';

/**
 * Écran d'entrée : on choisit son rôle et son identité avant d'accéder à la
 * plateforme.
 *
 * Conséquence directe : « saisi par » disparaît des formulaires. Il était
 * ressaisi à chaque valeur, en texte libre, ce qui produisait des variantes
 * d'orthographe du même nom. L'agent est désormais connu une fois pour toutes.
 *
 * Ce n'est PAS une authentification : aucun mot de passe, aucune vérification
 * côté serveur. C'est une identification déclarative, comme un registre de
 * saisie que l'on signe.
 */
@Component({
  selector: 'app-entree',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './entree.component.html',
})
export class EntreeComponent implements OnInit {
  private readonly roles = inject(RoleService);
  private readonly indicateurService = inject(IndicateurService);
  private readonly cdr = inject(ChangeDetectorRef);

  readonly listeRoles = ROLES;

  agents: Utilisateur[] = [];
  chargement = true;
  erreur = '';

  roleChoisi: Role = 'Administrateur';
  agentChoisi = '';

  ngOnInit(): void {
    this.indicateurService.getUtilisateurs().subscribe({
      next: (liste) => {
        this.agents = liste;
        this.chargement = false;
        this.preselectionner();
        this.cdr.markForCheck();
      },
      error: (err) => {
        console.error('Erreur lors du chargement des agents', err);
        // Le référentiel vient de la base : s'il est injoignable, on le dit
        // plutôt que de laisser un menu vide inexplicable.
        this.erreur =
          "Impossible de charger la liste des agents. Vérifie que le service métier et PostgreSQL sont démarrés.";
        this.chargement = false;
        this.cdr.markForCheck();
      },
    });
  }

  /** Agents correspondant au rôle choisi, sinon tous. */
  get agentsDuRole(): Utilisateur[] {
    const correspondants = this.agents.filter((a) => a.role === this.roleChoisi);
    return correspondants.length > 0 ? correspondants : this.agents;
  }

  surChangementRole(): void {
    this.preselectionner();
  }

  private preselectionner(): void {
    const possibles = this.agentsDuRole;
    this.agentChoisi = possibles.length > 0 ? possibles[0].nomUtilisateur : '';
  }

  entrer(): void {
    if (!this.agentChoisi) return;
    this.roles.demarrerSession(this.roleChoisi, this.agentChoisi);
  }

  descriptionRole(role: Role): string {
    switch (role) {
      case 'Agent de saisie':
        return 'Saisir et modifier des valeurs. Ne valide pas.';
      case 'Validateur':
        return "Valider, rejeter, dévalider : décide de ce que voit l'IA.";
      default:
        return 'Toutes les actions, y compris la suppression.';
    }
  }
}
