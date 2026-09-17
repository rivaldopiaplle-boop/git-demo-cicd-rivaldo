import { dateLisible, resumer, validerTitre } from "./taches.js";

const formulaire = document.getElementById("formulaire");
const champ = document.getElementById("titre");
const erreur = document.getElementById("erreur");
const liste = document.getElementById("liste");
const resume = document.getElementById("resume");
const etat = document.getElementById("etat");

/** nginx transmet /api au service back-end : le navigateur ne connaît qu'une adresse. */
const API = "/api";

async function rafraichirEtat() {
  try {
    const reponse = await fetch(`${API}/sante`);
    const sante = await reponse.json();
    const disponible = reponse.ok && sante.base === "disponible";
    etat.textContent = disponible ? "API et base de données disponibles." : "L'API répond, mais la base est indisponible.";
    etat.dataset.etat = disponible ? "ok" : "panne";
  } catch {
    etat.textContent = "L'API ne répond pas.";
    etat.dataset.etat = "panne";
  }
}

async function rafraichirListe() {
  const reponse = await fetch(`${API}/taches`);
  if (!reponse.ok) {
    erreur.textContent = "La liste n'a pas pu être chargée.";
    return;
  }
  const taches = await reponse.json();
  resume.textContent = resumer(taches);
  liste.replaceChildren(
    ...taches.map((tache) => {
      const item = document.createElement("li");
      const titre = document.createElement("span");
      titre.textContent = tache.titre;
      const date = document.createElement("span");
      date.className = "date";
      date.textContent = dateLisible(tache.creeeLe);
      item.append(titre, date);
      return item;
    }),
  );
}

formulaire.addEventListener("submit", async (evenement) => {
  evenement.preventDefault();
  const titre = champ.value;
  const probleme = validerTitre(titre);
  erreur.textContent = probleme ?? "";
  if (probleme) return;

  const reponse = await fetch(`${API}/taches`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ titre }),
  });

  if (!reponse.ok) {
    const corps = await reponse.json().catch(() => ({}));
    erreur.textContent = corps.erreur ?? "La tâche n'a pas pu être créée.";
    return;
  }

  champ.value = "";
  await rafraichirListe();
});

await rafraichirEtat();
await rafraichirListe();
