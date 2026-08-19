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

  readonly role = signal<Role>(this.lireRoleEnregistre());

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
    try {
      localStorage.setItem(RoleService.CLE_STOCKAGE, role);
    } catch {
      // Navigation privée ou stockage indisponible : le rôle reste en mémoire.
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
