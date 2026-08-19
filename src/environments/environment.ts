/**
 * URL de base des APIs.
 *
 * Vide = chemins relatifs (/api/...). C'est le mode normal :
 *  - en développement, `ng serve` relaie /api vers le Gateway via proxy.conf.json ;
 *  - en production, nginx (image Docker du front) relaie /api vers le service
 *    Kubernetes `gateway`.
 *
 * Conséquence : aucune adresse IP n'est codée en dur dans le code, et la même
 * image Docker fonctionne sur n'importe quelle VM sans être reconstruite.
 *
 * Ne renseigner une URL absolue (ex. 'http://192.168.153.131:5169') que pour
 * un test ponctuel contre un Gateway distant, sans proxy.
 */
export const environment = {
  production: false,
  apiBaseUrl: '',
};
