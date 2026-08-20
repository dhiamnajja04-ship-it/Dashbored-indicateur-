import { Injectable, computed, signal } from '@angular/core';

export type Role = 'Agent de saisie' | 'Validateur' | 'Administrateur';

export const ROLES: Role[] = ['Agent de saisie', 'Validateur', 'Administrateur'];

/**
 * Rôle courant de l'utilisateur.
 *
 * ATTENTION — ceci n'est PAS un mécanisme de sécurité. Il n'y a ni compte, ni
 * mot de passe, ni contrôle côté serveur : le rôle est choisi librement dans
 * l'interface et sert uniquement à adapter les actions proposées et à
 * renseigner l'auteur d'une validation.
 *
 * Une vraie séparation des droits demanderait une authentification et des
 * contrôles dans MetierService (voir livraisons/semaine-08).
 */
@Injectable({ providedIn: 'root' })
export class RoleService {
  private static readonly CLE_STOCKAGE = 'plateforme-indicateurs.role';
  private static readonly CLE_AGENT = 'plateforme-indicateurs.agent';

  readonly role = signal<Role>(this.lireRoleEnregistre());

  /**
   * Agent identifié à l'entrée, choisi dans le référentiel « utilisateurs ».
   * C'est lui qui alimente « saisi par » et « validé par » : le champ a été
   * retiré des formulaires, où il était ressaisi à chaque valeur.
   */
  readonly agent = signal<string>(this.lireAgentEnregistre());

  /**
   * Vrai quand un agent a été choisi. Tant que c'est faux, l'application
   * affiche l'écran d'entrée au lieu du contenu : on ne saisit pas de donnée
   * sans savoir qui la saisit.
   */
  readonly sessionEtablie = computed(() => this.agent().trim().length > 0);

  /** Saisir et modifier des valeurs. */
  readonly peutSaisir = computed(
    () => this.role() === 'Agent de saisie' || this.role() === 'Administrateur',
  );

  /** Valider, rejeter, dévalider : décide de ce que voit l'IA. */
  readonly peutValider = computed(
    () => this.role() === 'Validateur' || this.role() === 'Administrateur',
  );

  /** Supprimer définitivement un indicateur ou une valeur. */
  readonly peutSupprimer = computed(() => this.role() === 'Administrateur');

  /** Créer ou modifier la définition d'un indicateur. */
  readonly peutGererIndicateurs = computed(
    () => this.role() === 'Administrateur' || this.role() === 'Agent de saisie',
  );

  definirRole(role: Role): void {
    this.role.set(role);
    this.enregistrer(RoleService.CLE_STOCKAGE, role);
  }

  /** Ouvre la session de travail : rôle + agent identifié. */
  demarrerSession(role: Role, agent: string): void {
    this.role.set(role);
    this.agent.set(agent);
    this.enregistrer(RoleService.CLE_STOCKAGE, role);
    this.enregistrer(RoleService.CLE_AGENT, agent);
  }

  /** Referme la session : on repasse par l'écran d'entrée. */
  terminerSession(): void {
    this.agent.set('');
    this.enregistrer(RoleService.CLE_AGENT, '');
  }

  private enregistrer(cle: string, valeur: string): void {
    try {
      localStorage.setItem(cle, valeur);
    } catch {
      // Navigation privée ou stockage indisponible : l'état reste en mémoire.
    }
  }

  private lireAgentEnregistre(): string {
    try {
      return localStorage.getItem(RoleService.CLE_AGENT) ?? '';
    } catch {
      return '';
    }
  }

  private lireRoleEnregistre(): Role {
    try {
      const enregistre = localStorage.getItem(RoleService.CLE_STOCKAGE);
      if (enregistre && (ROLES as string[]).includes(enregistre)) {
        return enregistre as Role;
      }
    } catch {
      // Ignoré : on retombe sur le rôle par défaut.
    }
    return 'Administrateur';
  }
}
