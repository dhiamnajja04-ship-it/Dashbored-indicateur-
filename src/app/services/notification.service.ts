import { Injectable, signal } from '@angular/core';

export type TypeNotification = 'succes' | 'erreur' | 'info' | 'avertissement';

export interface Notification {
  id: number;
  type: TypeNotification;
  titre: string;
  message?: string;
  /** Durée d'affichage en ms, utilisée pour animer la barre de progression. */
  duree: number;
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
    succes: 6000,
    info: 7000,
    avertissement: 9000,
    erreur: 12000,
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
    const duree = this.dureeParType[type];
    this.notifications.update((liste) => [...liste, { id, type, titre, message, duree }]);
    setTimeout(() => this.fermer(id), duree);
  }
}
