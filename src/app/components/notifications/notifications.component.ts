import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, TypeNotification } from '../../services/notification.service';

/**
 * Affiche les notifications empilées en bas à droite de l'écran.
 * Purement présentationnel : la décision d'émettre vient des composants métier.
 */
@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './notifications.component.html',
})
export class NotificationsComponent {
  private readonly service = inject(NotificationService);

  readonly notifications = this.service.notifications;

  fermer(id: number): void {
    this.service.fermer(id);
  }

  icone(type: TypeNotification): string {
    switch (type) {
      case 'succes':
        return 'bi-check-circle-fill';
      case 'erreur':
        return 'bi-x-circle-fill';
      case 'avertissement':
        return 'bi-exclamation-triangle-fill';
      default:
        return 'bi-info-circle-fill';
    }
  }
}
