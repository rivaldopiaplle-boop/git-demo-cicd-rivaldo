# Chaîne CI/CD multi-conteneurs

[![Integration continue](https://github.com/rivaldopiaplle-boop/git-demo-cicd-rivaldo/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/rivaldopiaplle-boop/git-demo-cicd-rivaldo/actions/workflows/ci.yml?query=branch%3Amain)

Une chaîne d'intégration et de publication en quatre étages, et l'application qu'elle
vérifie : un front statique servi par nginx, une API .NET, une base MySQL. L'application
reste volontairement petite. Ce qui est démontré ici, c'est la chaîne.

Le badge ci-dessus donne l'état de la dernière exécution sur `main`, et le lien mène aux
exécutions elles-mêmes : chaque étage, ses journaux, sa durée.

## L'application

Un suivi de tâches qui se tient : lister, filtrer, créer, cocher, supprimer, refuser ce
qui n'est pas valide, et des compteurs calculés par la base.

| Partie | Contenu |
| --- | --- |
| `frontend/` | Page statique et module `taches.js` (règles d'affichage), servis par nginx, qui transmet `/api` au back-end |
| `backend/Api/` | API .NET 10 minimale : `/api/sante`, `/api/taches` (liste filtrable, création), `/api/taches/{id}` (lecture, `PATCH`, `DELETE`), `/api/taches/compteurs` |
| `backend/BackendTests/` | Tests unitaires des règles, et tests d'API contre une vraie base MySQL |
| `mysql/` | `schema.sql`, la seule source de la structure des données, et la migration qui l'applique |
| `k6/` | Tenue en charge avec k6, et parcours d'un visiteur dans un vrai Chrome |

`PATCH` ne change que ce qui est envoyé : cocher une tâche n'oblige pas à renvoyer son
titre. Le filtre se donne dans l'adresse (`?filtre=a-faire`, `?filtre=faites`), et une
valeur inconnue montre tout plutôt que de répondre par une erreur.

La règle du titre vit côté serveur. Le formulaire la reprend pour le confort, mais le
test du navigateur vérifie aussi qu'un appel direct à l'API, formulaire contourné, est
refusé avec un code 400.

## Lancer la pile en local

Prérequis : Docker et Node 20. Rien d'autre : le SDK .NET, nginx, MySQL et k6 vivent
dans les images.

```bash
node demarrer.mjs
```

Il vérifie Docker, crée `.env` au besoin, monte les trois conteneurs, applique le schéma,
attend que l'API réponde vraiment, puis donne l'adresse : http://localhost:8080.

```bash
node demarrer.mjs --tests     # la pile, puis la charge k6 et le parcours Chrome
node demarrer.mjs --arreter   # tout arrêter et supprimer les données
```

Le parcours Chrome prend le navigateur installé sur le poste. Dans la chaîne, c'est
`browser-actions/setup-chrome` qui le fournit.

À la main, si vous préférez voir chaque étape :

```bash
cp .env.example .env          # y poser MYSQL_ROOT_PASSWORD
docker compose -f docker-compose.build.yml up -d --build --wait
bash mysql/bootstrap-mysql.sh # applique le schéma
cd k6 && K6_BASE_URL=http://host.docker.internal:8080 bash run_integration.sh
```

`K6_BASE_URL` n'est utile que sur Windows et macOS : k6 tourne dans un conteneur, où
`localhost` désigne le conteneur lui-même. Sur Linux, l'adresse par défaut suffit.

Les tests du back-end, contre une base réelle :

```bash
docker run -d --name mysql-test -e MYSQL_ROOT_PASSWORD=motdepasse-local mysql:8.4
cd backend && dotnet test Backend.sln   # DB_URL, DB_USERNAME, DB_PASSWORD, DB_DATABASE
```

## Les quatre étages de la chaîne

| Étage | Ce qu'il vérifie | Ce qui le déclenche |
| --- | --- | --- |
| Tests du front | `node --test` sur les règles d'affichage | chaque poussée et chaque demande de fusion |
| Tests du back-end | tests unitaires, puis tests d'API contre un service MySQL | en parallèle du front |
| Intégration | la pile entière montée par docker compose, le schéma appliqué, 10 utilisateurs simultanés sous k6 (moins de 1 % d'échecs, 95 % des réponses sous 500 ms), puis le parcours complet dans Chrome | seulement si les deux étages précédents sont verts |
| Images | images front et back construites en matrice, publiées sur GHCR depuis `main` seulement | seulement si l'intégration est verte |

Deux détails qui comptent :

- **Les images ne portent jamais l'étiquette `latest`.** Elles sont nommées par le hash
  du commit, plus une étiquette `main`. Redéployer une version précise reste possible.
- **La sonde de santé interroge vraiment la base** (`SELECT 1`). Un pool de connexions
  configuré ne prouve pas qu'une base répond.

## Mettre en ligne

L'hébergement gratuit de Render ne propose pas de MySQL, et ses 750 heures
mensuelles sont partagées par tout un compte. [Northflank](https://northflank.com) offre,
lui, **deux services et une base de données gratuits, sans mise en veille** : exactement
la forme de cette pile.

1. Créer un compte, puis un projet, et y connecter ce dépôt GitHub.
2. **Base de données** : ajouter un *MySQL addon*. Northflank donne l'hôte, l'utilisateur,
   le mot de passe et le nom de la base.
3. **Service back-end** : construction par Dockerfile, fichier `backend/Dockerfile`,
   **contexte de construction à la racine du dépôt** (l'image embarque `mysql/schema.sql`).
   Port 8080. Variables d'environnement :

   | Variable | Valeur |
   | --- | --- |
   | `DB_URL` | l'hôte donné par l'addon |
   | `DB_PORT` | son port |
   | `DB_USERNAME`, `DB_PASSWORD`, `DB_DATABASE` | ce que l'addon fournit |
   | `APPLIQUER_SCHEMA` | `1` |

   `APPLIQUER_SCHEMA=1` fait appliquer `mysql/schema.sql` au démarrage : chez un
   hébergeur, personne n'ouvre de terminal pour lancer la migration.

4. **Service front** : Dockerfile `frontend/Dockerfile`, contexte `frontend/`, port 80,
   et une variable `ADRESSE_API` qui porte l'adresse interne du back-end, par exemple
   `http://backend:8080`. nginx remplit son gabarit au démarrage : la même image sert en
   local et en ligne.
5. Le front est le service exposé au public ; le back-end n'a pas besoin de l'être.

## Secrets

Le mot de passe de la base vient du secret `CI_CD_PASSWORD` du dépôt. En local, il vit
dans `.env`, qui n'est pas suivi par Git ; `.env.example` montre la forme attendue.
