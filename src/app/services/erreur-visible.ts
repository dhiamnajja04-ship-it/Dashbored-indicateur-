import { ErrorHandler, Injectable } from '@angular/core';

/**
 * Affiche les erreurs d'exécution DANS la page, en plus de la console.
 *
 * Utilité : quand l'interface reste blanche, l'erreur n'est visible que dans
 * la console du navigateur — inaccessible à distance. Ce gestionnaire la rend
 * lisible à l'écran, ce qui permet de diagnostiquer sur simple capture.
 */
@Injectable()
export class ErreurVisibleHandler implements ErrorHandler {
  handleError(erreur: unknown): void {
    console.error(erreur);

    try {
      const message =
        erreur instanceof Error
          ? `${erreur.name}: ${erreur.message}\n\n${(erreur.stack ?? '').split('\n').slice(0, 6).join('\n')}`
          : String(erreur);

      let boite = document.getElementById('erreur-visible');
      if (!boite) {
        boite = document.createElement('pre');
        boite.id = 'erreur-visible';
        boite.setAttribute(
          'style',
          'position:fixed;bottom:0;left:0;right:0;z-index:2000;max-height:45vh;overflow:auto;' +
            'margin:0;padding:12px 16px;background:#2b0b0b;color:#ffd7d7;font-size:12px;' +
            'line-height:1.5;white-space:pre-wrap;border-top:3px solid #d03b3b;',
        );
        document.body.appendChild(boite);
      }
      boite.textContent = `ERREUR APPLICATIVE\n\n${message}`;
    } catch {
      // Si même l'affichage échoue, la console reste la source.
    }
  }
}
