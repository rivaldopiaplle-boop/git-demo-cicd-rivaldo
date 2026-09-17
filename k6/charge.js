// Tenue en charge de l'API, à travers le front : dix utilisateurs simultanés
// créent et relisent des tâches pendant vingt secondes.
//
// Les seuils font échouer la chaîne d'eux-mêmes : sans seuil, un test de charge
// affiche des chiffres que personne ne lit.
import http from "k6/http";
import { check, sleep } from "k6";

const BASE = __ENV.BASE_URL || "http://localhost:8080";

export const options = {
  vus: 10,
  duration: "20s",
  thresholds: {
    http_req_failed: ["rate<0.01"],
    "http_req_duration{expected_response:true}": ["p(95)<500"],
    checks: ["rate>0.99"],
  },
};

export default function () {
  const creation = http.post(
    `${BASE}/api/taches`,
    JSON.stringify({ titre: `charge ${__VU}-${__ITER}` }),
    { headers: { "Content-Type": "application/json" } },
  );
  check(creation, { "création : 201": (r) => r.status === 201 });

  const liste = http.get(`${BASE}/api/taches`);
  check(liste, {
    "liste : 200": (r) => r.status === 200,
    "liste : au moins une tâche": (r) => Array.isArray(r.json()) && r.json().length > 0,
  });

  sleep(0.5);
}
