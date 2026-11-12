#!/usr/bin/env bash
# Essai d'une migration EF Core sur une base PostgreSQL éphémère (conteneur Docker jetable).
# À lancer depuis la racine de la copie (le dossier de ECommerce.slnx) :
#
#   bash .claude/skills/review-migration/essai-ephemere.sh <projet> [migration] [donnees.sql]
#
#   <projet>       dossier du projet qui contient les migrations, ex. src/ECommerce.Catalog.Api
#   [migration]    nom ou identifiant de la migration à essayer ; par défaut, la dernière
#   [donnees.sql]  SQL facultatif, exécuté avant la migration : des lignes « de production »
#
# Déroulé : migrations précédentes appliquées, lignes existantes copiées (des lignes qui ne
# viennent pas de HasData), état avant, montée, état après, descente, comparaison avec l'état
# avant. Le SQL généré est rangé dans artifacts/review-migration/<migration>/ (ignoré par Git).
# Le conteneur n'expose aucun port et il est supprimé à la fin, même en cas d'échec.
#
# Code de retour : 0 si la montée et la descente passent, 1 si une étape SQL échoue,
# 2 si l'essai ne peut pas tourner (Docker, dotnet ef, arguments).
set -uo pipefail

IMAGE="${IMAGE_POSTGRES:-postgres:17}" # la version des tests Testcontainers
DELAI_DEMARRAGE=60                     # secondes

projet="${1:-}"
demande="${2:-}"
donnees="${3:-}"
conteneur=""

echec_preparation() { echo "Essai impossible : $*" >&2; exit 2; }

[[ -f ECommerce.slnx ]] || echec_preparation "ECommerce.slnx introuvable ; lancez le script depuis la racine de la copie."
[[ -n "$projet" && -d "$projet" ]] || echec_preparation "indiquez le dossier du projet, ex. src/ECommerce.Catalog.Api."
[[ -z "$donnees" || -f "$donnees" ]] || echec_preparation "fichier de données introuvable : $donnees"
for outil in docker jq dotnet; do
  command -v "$outil" >/dev/null 2>&1 || echec_preparation "outil manquant : $outil"
done
docker info >/dev/null 2>&1 || echec_preparation "Docker ne répond pas ; démarrez Docker Desktop."
dotnet ef --version >/dev/null 2>&1 || echec_preparation "dotnet ef absent ; installez l'outil dotnet-ef."

titre() { printf '\n===== %s =====\n' "$*"; }

# dotnet ef écrit ses fichiers avec un BOM UTF-8, que psql refuse en début de script.
sans_bom() { LC_ALL=C sed $'1s/^\xef\xbb\xbf//' "$1"; }

# --- Les migrations du projet -----------------------------------------------------------
titre "Migrations de $projet"
# dotnet ef lit les métadonnées du projet avant de le compiler : il lui faut un projet restauré.
if [[ ! -f "$projet/obj/project.assets.json" ]]; then
  dotnet restore "$projet" >/dev/null 2>&1 || echec_preparation "dotnet restore $projet a échoué."
fi
if ! liste="$(DOTNET_CLI_UI_LANGUAGE=en dotnet ef migrations list --project "$projet" \
  --no-connect --json --prefix-output 2>&1)"; then
  echo "$liste" | tail -n 15 >&2
  echec_preparation "dotnet ef migrations list a échoué (le projet compile-t-il ?)."
fi
ids="$(sed -n 's/^data: *//p' <<<"$liste" | jq -r '.[].id')"
[[ -n "$ids" ]] || echec_preparation "aucune migration dans $projet."
echo "$ids"

cible="" precedente="0"
if [[ -z "$demande" ]]; then
  cible="$(tail -n 1 <<<"$ids")"
fi
while read -r id; do
  [[ -n "$cible" ]] && break
  if [[ "$id" == "$demande" || "${id#*_}" == "$demande" ]]; then cible="$id"; break; fi
  precedente="$id"
done <<<"$ids"
[[ -n "$cible" ]] || echec_preparation "migration « $demande » introuvable dans la liste ci-dessus."
if [[ -z "$demande" ]]; then
  precedente="$(grep -B1 -x -F "$cible" <<<"$ids" | grep -v -x -F "$cible" || true)"
  [[ -n "$precedente" ]] || precedente="0"
fi
echo "Migration essayée : $cible (précédente : $precedente)"

# --- Le SQL généré ------------------------------------------------------------------------
dossier="artifacts/review-migration/$cible"
mkdir -p "$dossier"
script_ef() { # script_ef <de> <à> <fichier>
  DOTNET_CLI_UI_LANGUAGE=en dotnet ef migrations script "$1" "$2" --project "$projet" \
    --no-build --output "$dossier/$3" >/dev/null 2>"$dossier/$3.erreurs" || {
    cat "$dossier/$3.erreurs" >&2
    echec_preparation "dotnet ef migrations script $1 $2 a échoué."
  }
  rm -f "$dossier/$3.erreurs"
}
[[ "$precedente" == "0" ]] || script_ef 0 "$precedente" 0-avant.sql
script_ef "$precedente" "$cible" 1-montee.sql
script_ef "$cible" "$precedente" 2-descente.sql

titre "SQL de la montée ($dossier/1-montee.sql)"
sans_bom "$dossier/1-montee.sql"
titre "SQL de la descente ($dossier/2-descente.sql)"
sans_bom "$dossier/2-descente.sql"

# --- La base éphémère ---------------------------------------------------------------------
arreter() {
  [[ -n "$conteneur" ]] && docker rm -f "$conteneur" >/dev/null 2>&1
  return 0
}
trap arreter EXIT

titre "Base éphémère ($IMAGE)"
docker image inspect "$IMAGE" >/dev/null 2>&1 || docker pull -q "$IMAGE" >/dev/null ||
  echec_preparation "impossible de récupérer l'image $IMAGE."
# Aucun port publié : la base n'est joignable que par docker exec, d'où l'absence de mot de passe.
conteneur="$(docker run -d --rm --name "review-migration-$$" --label lab-ecommerce=review-migration \
  -e POSTGRES_HOST_AUTH_METHOD=trust "$IMAGE")" || echec_preparation "docker run a échoué."
debut=$SECONDS
# Pendant l'initialisation, l'image lance un serveur temporaire sans TCP : attendre le TCP
# garantit que c'est le serveur définitif qui répond.
until docker exec "$conteneur" pg_isready -q -h 127.0.0.1 -U postgres >/dev/null 2>&1; do
  ((SECONDS - debut < DELAI_DEMARRAGE)) || echec_preparation "PostgreSQL ne répond pas après ${DELAI_DEMARRAGE} s."
  sleep 1
done
echo "PostgreSQL prêt en $((SECONDS - debut)) s : $(docker exec "$conteneur" postgres --version)"

psql_essai() { docker exec -i "$conteneur" psql -X -v ON_ERROR_STOP=1 -h 127.0.0.1 -U postgres -d postgres "$@"; }

tables() {
  psql_essai -At -c "SELECT tablename FROM pg_tables WHERE schemaname = 'public'
    AND tablename <> '__EFMigrationsHistory' ORDER BY tablename"
}

# Description, nombre de lignes et 10 premières lignes de chaque table.
etat() {
  local liste t
  liste="$(tables)"
  if [[ -z "$liste" ]]; then
    echo "(aucune table)"
    return
  fi
  while read -r t; do
    psql_essai -q -c "\\d \"$t\"" -c "SELECT count(*) AS lignes FROM \"$t\"" \
      -c "SELECT * FROM \"$t\" ORDER BY 1 LIMIT 10"
  done <<<"$liste"
}

# Empreintes comparables, indépendantes de l'ordre physique des colonnes et des lignes
# (une descente qui recrée une colonne la place en dernier ; un UPDATE déplace la ligne).
empreinte_schema() {
  psql_essai -At <<'SQL'
SELECT format('colonne %s.%s : %s%s%s%s', c.relname, a.attname, format_type(a.atttypid, a.atttypmod),
              CASE WHEN a.attnotnull THEN ' NOT NULL' ELSE '' END,
              CASE WHEN a.attidentity <> '' THEN ' IDENTITY' ELSE '' END,
              coalesce(' DEFAULT ' || pg_get_expr(d.adbin, d.adrelid), ''))
FROM pg_attribute a
JOIN pg_class c ON c.oid = a.attrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname <> '__EFMigrationsHistory'
  AND a.attnum > 0 AND NOT a.attisdropped
UNION ALL
SELECT format('index %s', indexdef) FROM pg_indexes
WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory'
UNION ALL
SELECT format('contrainte %s.%s : %s', conrelid::regclass, conname, pg_get_constraintdef(oid))
FROM pg_constraint
WHERE connamespace = 'public'::regnamespace AND conrelid <> 0
  AND conrelid::regclass::text <> '"__EFMigrationsHistory"'
ORDER BY 1;
SQL
}

empreinte_donnees() {
  local t
  while read -r t; do
    [[ -n "$t" ]] && psql_essai -At -c "SELECT '$t ' || to_jsonb(x)::text FROM \"$t\" x"
  done <<<"$(tables)" | sort
}

if [[ "$precedente" != "0" ]]; then
  titre "Migrations précédentes, jusqu'à $precedente"
  sans_bom "$dossier/0-avant.sql" | psql_essai -q >/dev/null || { echo "Échec des migrations précédentes." >&2; exit 1; }
  echo "Appliquées."

  titre "Lignes existantes hors graine (copie des lignes présentes)"
  # Les lignes de HasData ont des clés connues de la migration ; des copies n'en ont pas :
  # elles montrent ce que la migration fait aux autres lignes de la production.
  psql_essai -q <<'SQL'
DO $$
DECLARE
  t regclass;
  colonnes text;
  copiees bigint;
BEGIN
  FOR t IN SELECT c.oid FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
           WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname <> '__EFMigrationsHistory'
           ORDER BY c.relname
  LOOP
    SELECT string_agg(quote_ident(attname), ', ' ORDER BY attnum) INTO colonnes
    FROM pg_attribute
    WHERE attrelid = t AND attnum > 0 AND NOT attisdropped AND attidentity = '' AND attgenerated = '';
    BEGIN
      EXECUTE format('INSERT INTO %s (%s) SELECT %s FROM %s', t, colonnes, colonnes, t);
      GET DIAGNOSTICS copiees = ROW_COUNT;
      RAISE NOTICE '% : % ligne(s) copiée(s)', t, copiees;
    EXCEPTION WHEN others THEN
      RAISE NOTICE '% : copie impossible (%)', t, SQLERRM;
    END;
  END LOOP;
END $$;
SQL

  if [[ -n "$donnees" ]]; then
    titre "Données fournies ($donnees)"
    psql_essai -q -f - <"$donnees" || { echo "Échec du fichier de données." >&2; exit 1; }
    echo "Chargées."
  fi
elif [[ -n "$donnees" ]]; then
  echo "Données ignorées : avant la première migration, la base n'a aucune table."
fi

titre "État avant la migration"
etat
empreinte_schema >"$dossier/schema-avant.txt"
empreinte_donnees >"$dossier/donnees-avant.txt"

titre "Montée : $cible"
if ! { echo '\timing on'; sans_bom "$dossier/1-montee.sql"; } | psql_essai -e; then
  echo "MONTÉE EN ÉCHEC : la migration ne s'applique pas sur cette base." >&2
  exit 1
fi

titre "État après la montée"
etat

titre "Descente : retour à $precedente"
if ! { echo '\timing on'; sans_bom "$dossier/2-descente.sql"; } | psql_essai -e; then
  echo "DESCENTE EN ÉCHEC : Down() ne s'applique pas sur cette base." >&2
  exit 1
fi

titre "État après la descente"
etat

titre "Comparaison avec l'état avant la migration"
empreinte_schema >"$dossier/schema-apres-descente.txt"
empreinte_donnees >"$dossier/donnees-apres-descente.txt"
if diff -q "$dossier/schema-avant.txt" "$dossier/schema-apres-descente.txt" >/dev/null; then
  echo "Schéma : identique (Down() restaure le schéma)."
else
  echo "Schéma : DIFFÉRENT après la descente (< avant, > après) :"
  diff "$dossier/schema-avant.txt" "$dossier/schema-apres-descente.txt"
fi
if diff -q "$dossier/donnees-avant.txt" "$dossier/donnees-apres-descente.txt" >/dev/null; then
  echo "Données : identiques à l'état avant la migration."
else
  echo "Données : DIFFÉRENTES après la descente (< avant, > après) :"
  diff "$dossier/donnees-avant.txt" "$dossier/donnees-apres-descente.txt"
fi

titre "Résultat"
echo "ESSAI OK : montée et descente appliquées sur la base éphémère ; SQL dans $dossier/."
exit 0
