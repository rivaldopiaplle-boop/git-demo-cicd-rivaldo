-- Le schéma de l'application. Une seule source : la migration de la pile
-- (bootstrap-mysql.sh) et les tests d'intégration du back-end l'appliquent tous deux.
CREATE TABLE IF NOT EXISTS taches (
  id INT AUTO_INCREMENT PRIMARY KEY,
  titre VARCHAR(200) NOT NULL,
  faite BOOLEAN NOT NULL DEFAULT FALSE,
  creee_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);
