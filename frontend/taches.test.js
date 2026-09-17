import assert from "node:assert/strict";
import test from "node:test";
import { dateLisible, LONGUEUR_MAX, resumer, validerTitre } from "./taches.js";

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

test("le résumé compte les tâches et celles qui sont faites", () => {
  assert.equal(resumer([]), "0 tâche, dont 0 faite");
  assert.equal(resumer([{ faite: true }, { faite: false }]), "2 tâches, dont 1 faite");
});

test("une date invalide ne casse pas l'affichage", () => {
  assert.equal(dateLisible("pas une date"), "");
  assert.notEqual(dateLisible("2026-09-17T10:00:00Z"), "");
});
