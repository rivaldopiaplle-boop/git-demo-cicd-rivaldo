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

  // Cocher la tâche qu'on vient de créer : c'est l'écriture la plus fréquente
  // d'une liste de tâches, et elle passe par un UPDATE.
  const id = creation.status === 201 ? creation.json().id : null;
  if (id) {
    const coche = http.patch(`${BASE}/api/taches/${id}`, JSON.stringify({ faite: true }), {
      headers: { "Content-Type": "application/json" },
    });
    check(coche, { "coche : 200": (r) => r.status === 200 && r.json().faite === true });
  }

  const liste = http.get(`${BASE}/api/taches?filtre=a-faire`);
  check(liste, { "liste filtrée : 200": (r) => r.status === 200 && Array.isArray(r.json()) });

  const compteurs = http.get(`${BASE}/api/taches/compteurs`);
  check(compteurs, { "compteurs : 200": (r) => r.status === 200 && r.json().total > 0 });

  if (id) {
    const suppression = http.del(`${BASE}/api/taches/${id}`);
    check(suppression, { "suppression : 204": (r) => r.status === 204 });
  }

  sleep(0.5);
}
