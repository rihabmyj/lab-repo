---
name: review-migration
description: Relit une migration EF Core et le SQL qu'elle exécutera sur PostgreSQL (perte de données, valeur donnée aux lignes existantes, UpdateData qui écrase des lignes, verrous et réécritures de table, réversibilité de Down), l'essaie sur une base éphémère remplie de données, puis rend un verdict OK, à corriger ou bloquant. À utiliser après chaque dotnet ef migrations add, avant de commiter la migration ou de démarrer l'application.
argument-hint: "[projet] [migration]"
allowed-tools: Bash(dotnet ef migrations list *), Bash(dotnet ef migrations script *), Bash(dotnet ef migrations has-pending-model-changes *), Bash(bash *.claude/skills/review-migration/essai-ephemere.sh *), Bash(git status *), Bash(git diff *)
---

# Relecture d'une migration EF Core

Demande : $ARGUMENTS

Une migration générée compile toujours. La question est ailleurs : **que devient une base qui
contient déjà des données ?** On relit le SQL, pas seulement le C# : c'est lui qui s'exécutera.

Tu relis, tu ne corriges pas. Ne modifie aucun fichier, n'applique la migration à aucune base
existante (pas de `dotnet ef database update`, pas de démarrage de l'AppHost : `Program.cs`
appelle `Migrate()` au démarrage), n'entre dans aucun conteneur. Tu proposes ; la personne décide.

## 1. Identifier la migration

- **Projet** : le premier argument (`src/ECommerce.Catalog.Api`, `src/ECommerce.Ordering.Api`).
  Sans argument, cherche les migrations nouvelles avec `git status` (fichiers sous
  `Data/Migrations/`) ; s'il y en a dans deux projets, relis-les l'une après l'autre.
- **Migration** : le deuxième argument, sinon la dernière du projet
  (`dotnet ef migrations list --project <projet> --no-connect`).
- Lis le fichier de la migration (`Up` **et** `Down`), le diff du modèle (entités,
  `OnModelCreating`, `HasData`) et celui du snapshot (`git diff -- '<projet>/Data/Migrations/*ModelSnapshot.cs'`).
  Une migration qui n'est pas encore suivie par Git n'apparaît pas dans `git diff` : lis-la.
- Vérifie que le modèle et le snapshot sont alignés :
  `dotnet ef migrations has-pending-model-changes --project <projet>`.

## 2. L'essayer sur une base éphémère

Depuis la racine du dépôt, avec un délai d'attente de 5 minutes :

```bash
bash ${CLAUDE_SKILL_DIR}/essai-ephemere.sh <projet> <migration>
```

Le script génère le SQL de la montée et de la descente (`dotnet ef migrations script`), démarre
un PostgreSQL jetable (sans port publié, supprimé à la fin), applique les migrations
précédentes, **copie les lignes existantes** (des lignes que `HasData` ne connaît pas, comme en
production), puis affiche l'état avant, la montée avec le résultat de chaque instruction
(`UPDATE 1`, erreurs), l'état après, la descente et la comparaison avec l'état de départ.
Le SQL est rangé dans `artifacts/review-migration/<migration>/` (dossier ignoré par Git).

Si la table touchée est vide à ce stade (aucune graine, comme les commandes), écris un petit
jeu de données dans `artifacts/review-migration/<migration>.donnees.sql` : des `INSERT` avec la
liste des colonnes, valables **avant** la migration, avec des cas limites (valeurs nulles,
longues, doublons). Passe-le en troisième argument.

Code de retour : 0 si la montée et la descente passent, 1 si une instruction échoue (c'est un
constat bloquant, cite l'erreur), 2 si l'essai ne peut pas tourner (Docker, `dotnet ef`) : dis-le
et poursuis la relecture sur le SQL seul.

## 3. Liste de contrôle

Pour chaque point, cite la ligne du SQL ou la sortie de l'essai qui le prouve.

1. **Perte de données.** `DROP COLUMN`, `DROP TABLE`, `ALTER COLUMN … TYPE` vers un type plus
   étroit, renommage traduit en suppression puis ajout. EF affiche « An operation was scaffolded
   that may result in the loss of data » : dis quelle instruction il vise, et si la perte est réelle.
2. **Lignes existantes et colonnes obligatoires.** Une colonne `NOT NULL` ajoutée à une table
   remplie reçoit la valeur par défaut du SQL. `DEFAULT ''`, `DEFAULT 0` ou
   `DEFAULT '-infinity'` sont les valeurs factices d'EF, pas des valeurs métier : regarde
   dans l'état après la montée ce que reçoivent les lignes copiées.
3. **Données de `HasData`.** `InsertData`, `UpdateData` et `DeleteData` visent des lignes par
   leur clé, sans condition : en production, `UpdateData` **écrase** une valeur modifiée à la
   main, `DeleteData` supprime. Compte les `UPDATE n` de la montée et dis si c'est voulu.
4. **Verrous et réécritures** (le temps mesuré sur quelques lignes ne prouve rien : raisonne sur
   l'instruction). Toute la migration tourne dans une transaction : les verrous sont tenus
   jusqu'au `COMMIT`.

   | Instruction | Effet sur une grande table |
   |---|---|
   | `ADD COLUMN` sans défaut ou avec un défaut constant | Métadonnées seulement (PostgreSQL 11 et plus), instantané. |
   | `ADD COLUMN` avec un défaut volatil (`gen_random_uuid()`, `clock_timestamp()`) | Réécriture de la table sous verrou exclusif. |
   | `ALTER COLUMN … TYPE` | Réécriture sous verrou exclusif, sauf conversion binaire compatible (allonger un `varchar`). |
   | `ALTER COLUMN … SET NOT NULL` | Lecture de toute la table sous verrou exclusif. |
   | `CREATE INDEX` | Bloque les écritures pendant la construction. `CONCURRENTLY` l'évite, mais ne s'exécute pas dans une transaction. |
   | `ADD CONSTRAINT … FOREIGN KEY` | Vérifie toutes les lignes ; bloque les écritures sur les deux tables. |
   | `UPDATE` de masse | Verrou sur chaque ligne modifiée, durée proportionnelle au nombre de lignes. |

5. **Réversibilité.** `Down()` restaure-t-il le schéma (comparaison de l'essai) ? Que perd-il :
   les données écrites dans une colonne supprimée par la descente, des valeurs que la descente
   ne sait pas recréer ? Un `Down()` vide ou incomplet est un constat.
6. **Compatibilité avec la version en cours de l'application.** Pendant un déploiement, l'ancien
   code tourne sur le nouveau schéma : une colonne renommée ou supprimée le casse, une colonne
   obligatoire sans défaut fait échouer ses `INSERT`.
7. **Cohérence avec le code.** Une longueur maximale (`varchar(n)`) sans validation dans
   l'endpoint donne une erreur 500 au lieu d'un 400 ; un champ ajouté au JSON doit être reporté
   dans les DTO copiés (`CatalogProduct` d'Ordering, `ProductDto` du Web) si ces consommateurs
   en ont besoin. Une identité alimentée par `InsertData` doit être recalée (`setval`).

## 4. Restitution

```markdown
## Relecture de <migration> (<projet>)

| # | Gravité | Constat | Preuve | Correction proposée |
|---|---|---|---|---|
| 1 | Bloquant / À corriger / Remarque | … | ligne SQL ou sortie de l'essai | … |

**Essai sur base éphémère** : montée OK/échec, descente OK/échec, schéma restauré oui/non,
données restaurées oui/non (une ligne pour chaque).

**Verdict : OK | À CORRIGER | BLOQUANT**
```

- **Bloquant** : perte de données non voulue, montée ou descente en échec, verrou long sur une
  table chargée sans plan pour l'éviter.
- **À corriger** : valeur factice donnée aux lignes existantes, validation manquante,
  `Down()` incomplet, écrasement par `UpdateData` non assumé.
- **OK** : aucun constat, ou seulement des remarques que la personne a explicitement acceptées.

Pour une correction qui demande de régénérer la migration, rappelle l'ordre : retirer la
migration (elle ne doit avoir été appliquée nulle part), corriger le modèle, régénérer, relancer
`/review-migration`.
