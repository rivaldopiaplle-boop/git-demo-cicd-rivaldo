// Le parcours d'un vrai visiteur, dans un vrai Chrome : ouvrir la page, saisir
// une tâche, la voir apparaître. C'est le seul test qui prouve que le front, le
// proxy nginx, l'API et la base fonctionnent ensemble.
import puppeteer from "puppeteer-core";

const BASE = process.env.BASE_URL || "http://localhost:8080";
const titre = `Tâche vérifiée par Chrome ${Date.now()}`;

const navigateur = await puppeteer.launch({
  executablePath: process.env.PUPPETEER_EXECUTABLE_PATH,
  headless: true,
  args: ["--no-sandbox", "--disable-dev-shm-usage"],
});

try {
  const page = await navigateur.newPage();
  await page.goto(BASE, { waitUntil: "networkidle0" });

  const etat = await page.$eval("#etat", (n) => n.textContent.trim());
  console.log(`État annoncé par la page : ${etat}`);
  if (!etat.includes("disponibles")) throw new Error("La page n'annonce pas l'API et la base disponibles.");

  await page.type("#titre", titre);
  await page.click("button[type=submit]");
  await page.waitForFunction(
    (attendu) => [...document.querySelectorAll("#liste li")].some((li) => li.textContent.includes(attendu)),
    { timeout: 15000 },
    titre,
  );
  console.log("La tâche saisie dans le navigateur apparaît dans la liste.");

  // Le formulaire refuse un titre vide sans appeler le serveur.
  await page.click("button[type=submit]");
  const erreur = await page.$eval("#erreur", (n) => n.textContent.trim());
  if (erreur !== "Le titre est obligatoire.") throw new Error(`Message attendu absent, lu : « ${erreur} »`);
  console.log("Le formulaire refuse un titre vide.");

  // Et le serveur le refuse aussi, formulaire contourné.
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
