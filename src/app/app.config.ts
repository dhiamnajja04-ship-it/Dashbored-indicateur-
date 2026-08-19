import { ApplicationConfig, provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { renderInterceptor } from './interceptors/zone.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    // Zoneless : zone.js 0.16 est incompatible avec le backend Fetch de
    // HttpClient dans cette version d'Angular. Chargé, il fait avorter la
    // requête (AbortError) et l'Observable n'émet jamais — l'écran restait
    // bloqué sur « Chargement… ». Retiré, les réponses arrivent normalement.
    // Voir livraisons/semaine-08 pour le détail du diagnostic.
    provideZonelessChangeDetection(),
    provideRouter(routes),
    provideHttpClient(withInterceptors([renderInterceptor])),
  ],
};
