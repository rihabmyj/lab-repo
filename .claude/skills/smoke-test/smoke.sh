#!/usr/bin/env bash
# Test de fumée d'ECommerce. À lancer depuis la racine de la copie (le dossier de ECommerce.slnx).
#
# - Si la passerelle répond déjà, le test utilise l'application en cours et la laisse tourner.
# - Sinon, il démarre l'AppHost, attend la passerelle, puis arrête ce qu'il a démarré.
# - Trois vérifications passent par la passerelle : catalogue, commande valide, produit inconnu.
#
# Code de retour : 0 si tout passe, 1 si une vérification échoue, 2 si le test ne peut pas tourner.
set -uo pipefail

PASSERELLE="${PASSERELLE:-http://localhost:5067}"
DELAI_DEMARRAGE="${DELAI_DEMARRAGE:-240}" # secondes ; le premier lancement compile tout
PORT_PASSERELLE="${PASSERELLE##*:}"

racine="$(pwd -P)"
journal="${TMPDIR:-/tmp}/smoke-ecommerce-$$.log"
corps="$(mktemp)"
pid_run=""
echecs=0

[[ -f "$racine/ECommerce.slnx" ]] || {
  echo "ECommerce.slnx introuvable : lancez le script depuis la racine de votre copie." >&2
  exit 2
}
for outil in curl jq; do
  command -v "$outil" >/dev/null 2>&1 || { echo "Outil manquant : $outil" >&2; exit 2; }
done

code_http() { curl -s -o "$corps" -w '%{http_code}' --max-time 15 "$@" 2>/dev/null; }

passerelle_prete() { [[ "$(code_http "$PASSERELLE/catalog/api/products")" == 200 ]]; }

port_occupe() {
  if command -v lsof >/dev/null 2>&1; then
    lsof -nP -iTCP:"$PORT_PASSERELLE" -sTCP:LISTEN >/dev/null 2>&1
  else
    curl -s -o /dev/null --max-time 2 "$PASSERELLE" 2>/dev/null
    [[ $? -ne 7 ]] # 7 : connexion refusée, donc port libre
  fi
}

# Processus lancés depuis cette copie (AppHost, « dotnet run » des services, services).
processus_de_la_copie() { ps -eo pid=,command= | grep -F -- "$racine/src/ECommerce" | grep -v -F grep; }

arreter() {
  rm -f "$corps"
  [[ -n "$pid_run" ]] || return 0
  echo "Arrêt de l'application démarrée par le test..."
  # L'AppHost est le processus enfant de « dotnet run ». SIGTERM : un AppHost lancé en
  # arrière-plan ignore SIGINT. Il demande alors à DCP d'arrêter les services.
  local enfants i
  enfants="$(pgrep -P "$pid_run" 2>/dev/null || true)"
  if [[ -n "$enfants" ]]; then kill -TERM $enfants 2>/dev/null; fi
  kill -TERM "$pid_run" 2>/dev/null
  # DCP met une quinzaine de secondes à arrêter les services : tant qu'il en reste un,
  # un nouveau démarrage échouerait sur un port occupé.
  for i in $(seq 1 90); do
    if ! kill -0 "$pid_run" 2>/dev/null && ! port_occupe && [[ -z "$(processus_de_la_copie)" ]]; then
      echo "Application arrêtée en $i s : services terminés, port $PORT_PASSERELLE libre."
      return 0
    fi
    sleep 1
  done
  echo "Attention : des processus de la copie tournent encore :" >&2
  processus_de_la_copie >&2
}
trap arreter EXIT

# --- Démarrage, si nécessaire --------------------------------------------------------
if passerelle_prete; then
  echo "L'application tourne déjà : le test l'utilise et la laissera tourner."
elif port_occupe; then
  echo "Le port $PORT_PASSERELLE est occupé, mais la passerelle ne répond pas correctement." >&2
  echo "Une instance précédente s'arrête peut-être encore : attendez, puis relancez. (pgrep -fl ECommerce)" >&2
  exit 2
else
  echo "Démarrage de l'AppHost (build compris), journal : $journal"
  (cd "$racine" && exec dotnet run --project src/ECommerce.AppHost) >"$journal" 2>&1 &
  pid_run=$!
  debut=$SECONDS
  until passerelle_prete; do
    if ! kill -0 "$pid_run" 2>/dev/null; then
      echo "L'AppHost s'est arrêté avant d'être prêt. Fin du journal :" >&2
      tail -n 25 "$journal" >&2
      exit 2
    fi
    if ((SECONDS - debut > DELAI_DEMARRAGE)); then
      echo "La passerelle ne répond pas après ${DELAI_DEMARRAGE} s. Fin du journal :" >&2
      tail -n 25 "$journal" >&2
      exit 2
    fi
    sleep 2
  done
  echo "Passerelle prête en $((SECONDS - debut)) s."
fi

# --- Vérifications --------------------------------------------------------------------
printf '\n%-46s %-8s %-8s %s\n' "Vérification" "Attendu" "Obtenu" "Statut"
verifier() { # verifier <libellé> <code attendu> <code obtenu> [détail]
  local statut="OK"
  if [[ "$2" != "$3" ]]; then
    statut="ÉCHEC"
    echecs=$((echecs + 1))
  fi
  printf '%-46s %-8s %-8s %s\n' "$1" "$2" "$3" "$statut${4:+ ($4)}"
}

code="$(code_http "$PASSERELLE/catalog/api/products")"
nombre="$(jq 'if type == "array" then length else "?" end' "$corps" 2>/dev/null || echo "?")"
verifier "Catalogue : GET /catalog/api/products" 200 "$code" "$nombre produits"

code="$(code_http -X POST "$PASSERELLE/ordering/api/orders" -H 'Content-Type: application/json' \
  -d '{"customer":"Smoke","items":[{"productId":1,"quantity":2}]}')"
total="$(jq -r '.total // empty' "$corps" 2>/dev/null)"
verifier "Commande valide : POST /ordering/api/orders" 201 "$code" "${total:+total $total}"

code="$(code_http -X POST "$PASSERELLE/ordering/api/orders" -H 'Content-Type: application/json' \
  -d '{"customer":"Smoke","items":[{"productId":99,"quantity":1}]}')"
verifier "Produit inconnu : POST /ordering/api/orders" 400 "$code" "$(head -c 80 "$corps" 2>/dev/null)"

echo
if ((echecs == 0)); then
  echo "SMOKE OK"
  exit 0
fi
echo "SMOKE KO : $echecs vérification(s) en échec."
[[ -n "$pid_run" ]] && echo "Journal de l'AppHost : $journal (les logs des services sont dans le tableau de bord Aspire)."
exit 1
