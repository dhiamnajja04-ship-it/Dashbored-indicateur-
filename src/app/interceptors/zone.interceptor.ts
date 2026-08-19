import { HttpInterceptorFn } from '@angular/common/http';
import { ApplicationRef, inject } from '@angular/core';
import { tap } from 'rxjs';

/**
 * Déclenche un cycle de détection de changements après chaque réponse HTTP.
 *
 * L'application tourne en mode « zoneless » (voir app.config.ts) : sans
 * zone.js, Angular ne détecte plus automatiquement les mutations de propriétés
 * faites dans un callback `subscribe`. Les composants de ce projet utilisent des
 * propriétés simples (`loading`, `indicateurs`, …) et non des signaux : sans ce
 * tick, la donnée arrive bien mais la vue n'est jamais redessinée.
 *
 * Le tick est différé dans un `setTimeout` afin de s'exécuter après que le
 * callback du composant a mis à jour son état, et hors de tout cycle en cours.
 */
export const renderInterceptor: HttpInterceptorFn = (req, next) => {
  const appRef = inject(ApplicationRef);

  const planifierRendu = () =>
    setTimeout(() => {
      try {
        appRef.tick();
      } catch {
        // Un cycle déjà en cours redessinera la vue de toute façon.
      }
    }, 0);

  return next(req).pipe(
    tap({
      next: planifierRendu,
      error: planifierRendu,
    }),
  );
};
