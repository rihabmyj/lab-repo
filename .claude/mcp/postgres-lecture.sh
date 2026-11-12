#!/usr/bin/env bash
# Serveur MCP PostgreSQL : le secret reste dans le fichier fourni à Docker.
set -euo pipefail
base="${1:-}"
case "$base" in catalogdb|orderingdb) ;; *) echo 'Base autorisée : catalogdb ou orderingdb.' >&2; exit 2;; esac
fichier="$HOME/.config/lab-ecommerce/postgres-lecture.env"
[[ -r "$fichier" ]] || { echo 'Créez le rôle avec creer-role-lecture-seule.sh avant de lancer MCP.' >&2; exit 2; }
exec docker run -i --rm --env-file "$fichier" \
  --add-host host.docker.internal:host-gateway --env PGDATABASE="$base" \
  --entrypoint /bin/sh \
  "${IMAGE_POSTGRES_MCP:-crystaldba/postgres-mcp@sha256:dbbd346860d29f1543e991f30f3284bf4ab5f096d049ecc3426528f20b1b6e6b}" \
  -c 'export DATABASE_URI="postgresql://lecteur_mcp:${PGPASSWORD}@host.docker.internal:5433/${PGDATABASE}"; exec postgres-mcp --access-mode=restricted'
