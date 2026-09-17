// Le parcours d'un vrai visiteur, dans un vrai Chrome : créer une tâche, la
// cocher, la voir sortir du filtre « À faire », puis la supprimer. C'est le seul
// test qui prouve que le front, le proxy nginx, l'API et la base fonctionnent
// ensemble.
import puppeteer from "puppeteer-core";

const BASE = process.env.BASE_URL || "http://localhost:8080";
const titre = `Tâche vérifiée par Chrome ${Date.now()}`;
const dansUneChaine = Boolean(process.env.CI);

// La chaîne installe Chrome et donne son chemin ; sur un poste de travail, on
// prend le Chrome déjà installé, pour n'avoir rien à poser avant de lancer.
const chemin = process.env.PUPPETEER_EXECUTABLE_PATH;

let navigateur;
try {
  navigateur = await puppeteer.launch({
    ...(chemin ? { executablePath: chemin } : { channel: "chrome" }),
    headless: true,
    args: ["--no-sandbox", "--disable-dev-shm-usage"],
  });
} catch (erreur) {
  // Sans navigateur, ce test ne veut rien dire. Dans la chaîne, c'est une
  // régression : Chrome y est installé. Sur un poste sans Chrome, on le dit et
  // on laisse passer, plutôt que de faire croire à un échec de l'application.
  const message = `Chrome est introuvable (${erreur.message.split("\n")[0]}).`;
  if (dansUneChaine) {
    console.error(message);
    process.exit(1);
  }
  console.log(`${message}\nParcours navigateur ignoré. Pour le jouer : installer Chrome, ou poser PUPPETEER_EXECUTABLE_PATH vers un autre navigateur Chromium (Edge, Brave, Chromium).`);
  process.exit(0);
}

try {
  const page = await navigateur.newPage();
  await page.goto(BASE, { waitUntil: "networkidle0" });

  const etat = await page.$eval("#etat", (n) => n.textContent.trim());
  console.log(`État annoncé par la page : ${etat}`);
  if (!etat.includes("disponibles")) throw new Error("La page n'annonce pas l'API et la base disponibles.");

  // 1. Créer.
  await page.type("#titre", titre);
  await page.click("button[type=submit]");
  await page.waitForFunction(
    (attendu) => [...document.querySelectorAll("#liste li")].some((li) => li.textContent.includes(attendu)),
    { timeout: 15000 },
    titre,
  );
  console.log("La tâche saisie dans le navigateur apparaît dans la liste.");

  // 2. Cocher, et vérifier que le compteur et la ligne suivent.
  await page.evaluate((attendu) => {
    const item = [...document.querySelectorAll("#liste li")].find((li) => li.textContent.includes(attendu));
    item.querySelector("input[type=checkbox]").click();
  }, titre);
  // La liste est redessinée après la réponse du serveur : on attend la classe,
  // pas un délai.
  await page.waitForFunction(
    (attendu) => [...document.querySelectorAll("#liste li")].find((li) => li.textContent.includes(attendu))?.classList.contains("faite"),
    { timeout: 15000 },
    titre,
  );
  console.log(`Tâche cochée, compteurs à jour : ${await page.$eval("#resume", (n) => n.textContent.trim())}`);

  // 3. Le filtre « À faire » ne la montre plus.
  await page.click('button[data-filtre="a-faire"]');
  await page.waitForFunction(
    (attendu) => ![...document.querySelectorAll("#liste li")].some((li) => li.textContent.includes(attendu)),
    { timeout: 15000 },
    titre,
  );
  console.log("Le filtre « À faire » ne montre plus la tâche cochée.");

  // 4. Supprimer, depuis le filtre « Faites ».
  await page.click('button[data-filtre="faites"]');
  await page.waitForFunction(
    (attendu) => [...document.querySelectorAll("#liste li")].some((li) => li.textContent.includes(attendu)),
    { timeout: 15000 },
    titre,
  );
  await page.evaluate((attendu) => {
    const item = [...document.querySelectorAll("#liste li")].find((li) => li.textContent.includes(attendu));
    item.querySelector("button.supprimer").click();
  }, titre);
  await page.waitForFunction(
    (attendu) => ![...document.querySelectorAll("#liste li")].some((li) => li.textContent.includes(attendu)),
    { timeout: 15000 },
    titre,
  );
  console.log("La tâche supprimée disparaît de la liste.");

  // 5. Le formulaire refuse un titre vide sans appeler le serveur.
  await page.click('button[data-filtre="toutes"]');
  await page.click("button[type=submit]");
  const erreur = await page.$eval("#erreur", (n) => n.textContent.trim());
  if (erreur !== "Le titre est obligatoire.") throw new Error(`Message attendu absent, lu : « ${erreur} »`);
  console.log("Le formulaire refuse un titre vide.");

  // 6. Et le serveur le refuse aussi, formulaire contourné.
  const refus = await page.evaluate(async () => {
    const reponse = await fetch("/api/taches", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ titre: "   " }),
    });
    return reponse.status;
  });
  if (refus !== 400) throw new Error(`Le serveur a répondu ${refus} au lieu de 400.`);
  console.log("Le serveur refuse un titre vide, formulaire contourné.");
} finally {
  await navigateur.close();
}
