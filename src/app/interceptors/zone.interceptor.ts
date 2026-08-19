import { HttpInterceptorFn } from '@angular/common/http';
import { ApplicationRef, NgZone, inject } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * Force chaque réponse HTTP à être ré-émise à l'intérieur de la zone Angular,
 * PUIS force explicitement un cycle de détection de changements complet.
 *
 * Bug connu : le backend Fetch de HttpClient (utilisé par défaut par cette
 * version d'Angular) résout ses promesses en dehors de la zone patchée par
 * zone.js. Un simple `zone.run()` ne suffit pas toujours à faire réafficher
 * l'écran : la donnée arrive bien (visible dans l'onglet Network), l'état du
 * composant change bien (`loading = false`), mais Angular ne redessine pas
 * la vue. On appelle donc explicitement `ApplicationRef.tick()` après chaque
 * émission, ce qui force le rendu quel que soit le mécanisme de détection en
 * jeu — solution radicale mais fiable.
 */
export const zoneInterceptor: HttpInterceptorFn = (req, next) => {
  const zone = inject(NgZone);
  const appRef = inject(ApplicationRef);

  const forceRender = () => {
    try {
      appRef.tick();
    } catch {
      // tick() peut lever si un cycle est déjà en cours ; sans conséquence ici.
    }
  };

  return new Observable((subscriber) => {
    const subscription = next(req).subscribe({
      next: (event) =>
        zone.run(() => {
          subscriber.next(event);
          forceRender();
        }),
      error: (err) =>
        zone.run(() => {
          subscriber.error(err);
          forceRender();
        }),
      complete: () => zone.run(() => subscriber.complete()),
    });
    return () => subscription.unsubscribe();
  });
};
