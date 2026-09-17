import assert from "node:assert/strict";
import test from "node:test";
import { dateLisible, filtreValide, FILTRES, LONGUEUR_MAX, resumer, validerTitre } from "./taches.js";

test("un titre vide est refusé", () => {
  for (const titre of [undefined, "", "   "]) {
    assert.equal(validerTitre(titre), "Le titre est obligatoire.");
  }
});

test("un titre trop long est refusé", () => {
  assert.equal(validerTitre("a".repeat(LONGUEUR_MAX + 1)), `Le titre dépasse ${LONGUEUR_MAX} caractères.`);
});

test("les espaces autour ne comptent pas", () => {
  assert.equal(validerTitre(`  ${"a".repeat(LONGUEUR_MAX)}  `), null);
});

test("le résumé compte ce qui reste à faire", () => {
  assert.equal(resumer(), "Aucune tâche pour l'instant");
  assert.equal(resumer({ total: 0, faites: 0 }), "Aucune tâche pour l'instant");
  assert.equal(resumer({ total: 1, faites: 0 }), "1 tâche, 1 à faire, 0 faite");
  assert.equal(resumer({ total: 5, faites: 2 }), "5 tâches, 3 à faire, 2 faites");
});

test("un filtre inconnu montre tout", () => {
  for (const { id } of FILTRES) assert.equal(filtreValide(id), id);
  assert.equal(filtreValide("n-importe-quoi"), "toutes");
  assert.equal(filtreValide(undefined), "toutes");
});

test("une date invalide ne casse pas l'affichage", () => {
  assert.equal(dateLisible("pas une date"), "");
  assert.notEqual(dateLisible("2026-09-17T10:00:00Z"), "");
});
