// Les règles que le formulaire applique pour le confort du visiteur. Le serveur
// les applique de son côté : un formulaire contourné ne doit rien pouvoir créer.
export const LONGUEUR_MAX = 200;

export function validerTitre(titre) {
  const propre = (titre ?? "").trim();
  if (!propre) return "Le titre est obligatoire.";
  if (propre.length > LONGUEUR_MAX) return `Le titre dépasse ${LONGUEUR_MAX} caractères.`;
  return null;
}

export function resumer(taches) {
  const faites = taches.filter((t) => t.faite).length;
  const s = taches.length > 1 ? "s" : "";
  return `${taches.length} tâche${s}, dont ${faites} faite${faites > 1 ? "s" : ""}`;
}

export function dateLisible(valeur) {
  const date = new Date(valeur);
  if (Number.isNaN(date.getTime())) return "";
  return date.toLocaleString("fr-FR", { dateStyle: "short", timeStyle: "short" });
}
