// =====================================================================
// Scénario de charge k6 — alternative à hey, mentionnée dans le guide.
//
//   k6 run ci/charge-k6.js
//
// Monte progressivement à 50 utilisateurs simultanés, tient 30 s, puis
// redescend. Les seuils font échouer le test si la plateforme se dégrade,
// ce qu'un simple compteur de requêtes ne montrerait pas.
// =====================================================================
import http from 'k6/http';
import { check } from 'k6';

export const options = {
  stages: [
    { duration: '10s', target: 20 },
    { duration: '30s', target: 50 },
    { duration: '10s', target: 0 },
  ],
  thresholds: {
    // 95 % des requêtes sous 500 ms, et moins de 1 % d'échec.
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

const BASE = __ENV.BASE || 'http://localhost:30169';

export default function () {
  const reponse = http.get(`${BASE}/health`);

  check(reponse, {
    'statut 200': (r) => r.status === 200,
    // Le champ « instance » nomme le pod : sa présence prouve que la
    // répartition reste observable pendant la charge.
    'instance renseignée': (r) => {
      try {
        return JSON.parse(r.body).instance?.length > 0;
      } catch {
        return false;
      }
    },
  });
}
