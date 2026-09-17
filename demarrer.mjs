/**
 * Lancer la pile en une commande.
 *
 *   node demarrer.mjs
 *
 * Il vérifie Docker, monte les trois conteneurs, applique le schéma, attend que
 * l'API réponde vraiment, puis donne l'adresse. Rien à installer d'autre : le
 * SDK .NET et Node vivent dans les images.
 *
 *   node demarrer.mjs --tests    monte la pile puis joue les tests d'intégration
 *   node demarrer.mjs --arreter  arrête tout et supprime les données
 */
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, writeFileSync } from "node:fs";

const COMPOSE = ["compose", "-f", "docker-compose.build.yml"];
const options = process.argv.slice(2);
const veutTests = options.includes("--tests");
const veutArreter = options.includes("--arreter");

/** Le mot de passe local vit dans .env, qui n'est pas suivi par Git. */
function motDePasse() {
  if (process.env.MYSQL_ROOT_PASSWORD) return process.env.MYSQL_ROOT_PASSWORD;

  if (!existsSync(".env")) {
    writeFileSync(".env", readFileSync(".env.example", "utf8"));
    console.log("  .env créé depuis .env.example");
  }
  const ligne = readFileSync(".env", "utf8")
    .split("\n")
    .map((l) => l.trim())
    .find((l) => l.startsWith("MYSQL_ROOT_PASSWORD="));
  if (!ligne) {
    console.error("  .env ne contient pas MYSQL_ROOT_PASSWORD. Voir .env.example.");
    process.exit(1);
  }
  return ligne.slice("MYSQL_ROOT_PASSWORD=".length).trim();
}

function lancer(commande, arguments_, environnement = {}) {
  const resultat = spawnSync(commande, arguments_, {
    stdio: "inherit",
    // shell: false, pour éviter la concaténation des arguments (avertissement DEP0190).
    shell: false,
    env: { ...process.env, ...environnement },
  });
  return resultat.status === 0;
}

function verifierDocker() {
  const resultat = spawnSync("docker", ["version", "--format", "{{.Server.Version}}"], { encoding: "utf8" });
  if (resultat.status !== 0) {
    console.error("  Docker ne répond pas. Ouvrez Docker Desktop, puis relancez.");
    process.exit(1);
  }
  console.log(`  OK  Docker ${resultat.stdout.trim()}`);
}

async function attendreApi(adresse) {
  for (let essai = 0; essai < 90; essai += 1) {
    try {
      const reponse = await fetch(`${adresse}/api/sante`, { signal: AbortSignal.timeout(3000) });
      const sante = await reponse.json();
      if (sante.base === "disponible") return true;
    } catch {
      // La pile démarre encore.
    }
    await new Promise((r) => setTimeout(r, 1000));
  }
  return false;
}

console.log("\n  Chaîne CI/CD multi-conteneurs : front nginx, API .NET, base MySQL\n");
const MYSQL_ROOT_PASSWORD = motDePasse();

if (veutArreter) {
  lancer("docker", [...COMPOSE, "down", "-v"], { MYSQL_ROOT_PASSWORD });
  console.log("\n  Pile arrêtée, données supprimées.\n");
  process.exit(0);
}

verifierDocker();

console.log("\n-- Construction et démarrage");
if (!lancer("docker", [...COMPOSE, "up", "-d", "--build", "--wait"], { MYSQL_ROOT_PASSWORD })) {
  console.error("\n  La pile n'a pas démarré. Journaux : docker compose -f docker-compose.build.yml logs\n");
  process.exit(1);
}

console.log("\n-- Schéma de la base");
if (!lancer("bash", ["mysql/bootstrap-mysql.sh"], { MYSQL_ROOT_PASSWORD })) {
  console.error("\n  Le schéma n'a pas pu être appliqué.\n");
  process.exit(1);
}

const ADRESSE = "http://localhost:8080";
console.log("\n-- Attente de l'API");
if (!(await attendreApi(ADRESSE))) {
  console.error("\n  L'API n'a pas répondu. Journaux : docker compose -f docker-compose.build.yml logs backend\n");
  process.exit(1);
}
console.log("  OK  L'API répond et la base est disponible");

if (veutTests) {
  console.log("\n-- Tests d'intégration");
  const k6 = process.platform === "linux" ? ADRESSE : "http://host.docker.internal:8080";
  if (!lancer("bash", ["k6/run_integration.sh"], { BASE_URL: ADRESSE, K6_BASE_URL: k6, MYSQL_ROOT_PASSWORD })) {
    console.error("\n  Les tests d'intégration ont échoué.\n");
    process.exit(1);
  }
}

console.log(`\n  Ouvrez ${ADRESSE}`);
console.log("  Tests d'intégration : node demarrer.mjs --tests");
console.log("  Pour tout arrêter  : node demarrer.mjs --arreter\n");
