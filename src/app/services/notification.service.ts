import { Injectable, signal } from '@angular/core';

export type TypeNotification = 'succes' | 'erreur' | 'info' | 'avertissement';

export interface Notification {
  id: number;
  type: TypeNotification;
  titre: string;
  message?: string;
}

/**
 * File de notifications affichées en surimpression (toasts).
 *
 * Les composants signalent ici le résultat d'une action ; l'affichage est
 * centralisé dans NotificationsComponent. Aucune règle métier n'est portée par
 * ce service : il ne fait que transporter un message déjà décidé ailleurs.
 *
 * L'état est un `signal`, ce qui déclenche la détection de changements même en
 * mode zoneless (voir app.config.ts).
 */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private compteur = 0;

  /** Notifications actuellement visibles, de la plus ancienne à la plus récente. */
  readonly notifications = signal<Notification[]>([]);

  /** Durée d'affichage : une erreur reste plus longtemps, on doit pouvoir la lire. */
  private dureeParType: Record<TypeNotification, number> = {
    succes: 4000,
    info: 5000,
    avertissement: 7000,
    erreur: 9000,
  };

  succes(titre: string, message?: string): void {
    this.ajouter('succes', titre, message);
  }

  erreur(titre: string, message?: string): void {
    this.ajouter('erreur', titre, message);
  }

  info(titre: string, message?: string): void {
    this.ajouter('info', titre, message);
  }

  avertissement(titre: string, message?: string): void {
    this.ajouter('avertissement', titre, message);
  }

  fermer(id: number): void {
    this.notifications.update((liste) => liste.filter((n) => n.id !== id));
  }

  private ajouter(type: TypeNotification, titre: string, message?: string): void {
    const id = ++this.compteur;
    this.notifications.update((liste) => [...liste, { id, type, titre, message }]);
    setTimeout(() => this.fermer(id), this.dureeParType[type]);
  }
}
