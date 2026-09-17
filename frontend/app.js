import { dateLisible, FILTRES, filtreValide, resumer, validerTitre } from "./taches.js";

const formulaire = document.getElementById("formulaire");
const champ = document.getElementById("titre");
const erreur = document.getElementById("erreur");
const liste = document.getElementById("liste");
const resume = document.getElementById("resume");
const etat = document.getElementById("etat");
const zoneFiltres = document.getElementById("filtres");
const vide = document.getElementById("vide");

/** nginx transmet /api au service back-end : le navigateur ne connaît qu'une adresse. */
const API = "/api";
let filtre = "toutes";

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

function dessinerFiltres() {
  zoneFiltres.replaceChildren(
    ...FILTRES.map(({ id, nom }) => {
      const bouton = document.createElement("button");
      bouton.type = "button";
      bouton.textContent = nom;
      bouton.dataset.filtre = id;
      bouton.className = id === filtre ? "filtre actif" : "filtre";
      bouton.setAttribute("aria-pressed", String(id === filtre));
      bouton.addEventListener("click", async () => {
        filtre = filtreValide(id);
        dessinerFiltres();
        await rafraichirListe();
      });
      return bouton;
    }),
  );
}

function ligneTache(tache) {
  const item = document.createElement("li");
  if (tache.faite) item.className = "faite";

  const coche = document.createElement("input");
  coche.type = "checkbox";
  coche.checked = tache.faite;
  coche.id = `tache-${tache.id}`;
  coche.addEventListener("change", () => changer(tache.id, { faite: coche.checked }));

  const etiquette = document.createElement("label");
  etiquette.htmlFor = coche.id;
  etiquette.textContent = tache.titre;

  const date = document.createElement("span");
  date.className = "date";
  date.textContent = dateLisible(tache.creeeLe);

  const supprimer = document.createElement("button");
  supprimer.type = "button";
  supprimer.className = "supprimer";
  supprimer.title = "Supprimer cette tâche";
  supprimer.setAttribute("aria-label", `Supprimer : ${tache.titre}`);
  supprimer.textContent = "×";
  supprimer.addEventListener("click", () => effacer(tache.id));

  item.append(coche, etiquette, date, supprimer);
  return item;
}

async function rafraichirListe() {
  const [reponseListe, reponseCompteurs] = await Promise.all([
    fetch(`${API}/taches?filtre=${filtre}`),
    fetch(`${API}/taches/compteurs`),
  ]);

  if (!reponseListe.ok || !reponseCompteurs.ok) {
    erreur.textContent = "La liste n'a pas pu être chargée.";
    return;
  }

  const taches = await reponseListe.json();
  resume.textContent = resumer(await reponseCompteurs.json());
  vide.hidden = taches.length > 0;
  liste.replaceChildren(...taches.map(ligneTache));
}

async function changer(id, changement) {
  const reponse = await fetch(`${API}/taches/${id}`, {
    method: "PATCH",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(changement),
  });
  if (!reponse.ok) erreur.textContent = "La tâche n'a pas pu être modifiée.";
  await rafraichirListe();
}

async function effacer(id) {
  const reponse = await fetch(`${API}/taches/${id}`, { method: "DELETE" });
  if (!reponse.ok) erreur.textContent = "La tâche n'a pas pu être supprimée.";
  await rafraichirListe();
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

dessinerFiltres();
await rafraichirEtat();
await rafraichirListe();
