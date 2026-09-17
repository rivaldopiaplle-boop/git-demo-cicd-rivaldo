// Les règles que le formulaire applique pour le confort du visiteur. Le serveur
// les applique de son côté : un formulaire contourné ne doit rien pouvoir créer.
export const LONGUEUR_MAX = 200;

export const FILTRES = [
  { id: "toutes", nom: "Toutes" },
  { id: "a-faire", nom: "À faire" },
  { id: "faites", nom: "Faites" },
];

export function validerTitre(titre) {
  const propre = (titre ?? "").trim();
  if (!propre) return "Le titre est obligatoire.";
  if (propre.length > LONGUEUR_MAX) return `Le titre dépasse ${LONGUEUR_MAX} caractères.`;
  return null;
}

export function resumer({ total = 0, faites = 0 } = {}) {
  if (total === 0) return "Aucune tâche pour l'instant";
  const aFaire = total - faites;
  // « à faire » est invariable : seuls « tâche » et « faite » prennent un s.
  const pluriel = (nombre) => (nombre > 1 ? "s" : "");
  return `${total} tâche${pluriel(total)}, ${aFaire} à faire, ${faites} faite${pluriel(faites)}`;
}

/** Le filtre demandé au serveur ; une valeur inconnue montre tout. */
export function filtreValide(id) {
  return FILTRES.some((f) => f.id === id) ? id : "toutes";
}

export function dateLisible(valeur) {
  const date = new Date(valeur);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleString("fr-FR", { dateStyle: "short", timeStyle: "short" });
}
