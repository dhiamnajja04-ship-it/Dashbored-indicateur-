import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { NotificationsComponent } from './components/notifications/notifications.component';
import { EntreeComponent } from './components/entree/entree.component';
import { FooterComponent } from './components/footer/footer.component';
import { RoleService, ROLES, Role } from './services/role.service';
import { NotificationService } from './services/notification.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterModule, NotificationsComponent, FooterComponent, EntreeComponent],
  templateUrl: './app.component.html',
})
export class AppComponent {
  private readonly roleService = inject(RoleService);
  private readonly notifications = inject(NotificationService);

  readonly title = 'dashboard-indicateurs';
  readonly anneeCourante = new Date().getFullYear();
  readonly roles = ROLES;
  readonly role = this.roleService.role;

  /** Agent identifié à l'entrée ; alimente « saisi par » et « validé par ». */
  readonly agent = this.roleService.agent;

  /** Tant qu'aucun agent n'est identifié, l'écran d'entrée remplace le site. */
  readonly sessionEtablie = this.roleService.sessionEtablie;

  /** Referme la session : retour à l'écran d'identification. */
  quitter(): void {
    this.roleService.terminerSession();
  }

  changerRole(role: Role): void {
    if (role === this.role()) return;
    this.roleService.definirRole(role);
    this.notifications.info(`Rôle : ${role}`, this.descriptionRole(role));
  }

  descriptionRole(role: Role): string {
    switch (role) {
      case 'Agent de saisie':
        return 'Vous pouvez saisir des valeurs et les soumettre à validation.';
      case 'Validateur':
        return 'Vous pouvez valider, rejeter et dévalider les valeurs.';
      default:
        return 'Accès complet, y compris la suppression.';
    }
  }

  iconeRole(role: Role): string {
    switch (role) {
      case 'Agent de saisie':
        return 'bi-pencil-square';
      case 'Validateur':
        return 'bi-patch-check';
      default:
        return 'bi-shield-lock';
    }
  }
}
