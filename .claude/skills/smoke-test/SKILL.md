---
name: smoke-test
description: Test de fumée de l'application ECommerce. Démarre l'application si elle ne tourne pas, vérifie via la passerelle le catalogue, une commande valide et une commande refusée, puis arrête ce qu'il a démarré. À utiliser pour prouver que l'application fonctionne de bout en bout, après une modification et avant de conclure.
allowed-tools: Bash(bash *.claude/skills/smoke-test/smoke.sh)
---

# Test de fumée ECommerce

Depuis la racine du dépôt (le dossier qui contient `ECommerce.slnx`), lance le script du skill
avec un délai d'attente de 10 minutes : le premier lancement compile toute la solution.

```bash
bash ${CLAUDE_SKILL_DIR}/smoke.sh
```

Le script démarre l'AppHost seulement si la passerelle (`http://localhost:5067`) ne répond pas,
et dans ce cas l'arrête à la fin. Ne lance pas l'AppHost toi-même.

## Restitution

1. Recopie le tableau des vérifications tel quel, puis la dernière ligne : `SMOKE OK` ou `SMOKE KO`.
2. Si une vérification échoue, ne corrige rien. Donne la cause la plus probable en citant la
   sortie : code HTTP obtenu, message d'erreur et, si l'application n'a pas démarré, la fin du
   journal de l'AppHost affichée par le script.
3. Rappelle que les logs des services n'apparaissent pas dans la console de l'AppHost : ils sont
   dans le tableau de bord Aspire (logs de console, logs structurés, traces).
