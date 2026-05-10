#!/usr/bin/env bash
# Deploy / redeploy the tech-store stack on the Oracle VM.
# Pulls the latest main, rebuilds containers, applies migrations on startup,
# verifies health.

set -euo pipefail

APP_DIR="${APP_DIR:-/home/ubuntu/ballastlane-tech-store}"
COMPOSE=(docker compose -f "${APP_DIR}/docker-compose.yml" -f "${APP_DIR}/docker-compose.prod.yml")

cd "${APP_DIR}"

echo "==> git pull"
git fetch --all --prune
git reset --hard origin/main

echo "==> Loading .env"
if [ ! -f "${APP_DIR}/.env" ]; then
    echo "    .env missing — run bootstrap.sh first or copy deploy/.env.production.example"
    exit 1
fi
set -a; . "${APP_DIR}/.env"; set +a

echo "==> Building images"
"${COMPOSE[@]}" build --pull

echo "==> Bringing the stack up"
"${COMPOSE[@]}" up -d

echo "==> Waiting for APIs to report healthy"
for url in http://127.0.0.1:5101/health http://127.0.0.1:5102/health http://127.0.0.1:5174/; do
    for attempt in $(seq 1 30); do
        if curl -fsS --max-time 2 "${url}" >/dev/null 2>&1; then
            echo "    OK ${url}"
            break
        fi
        if [ "${attempt}" -eq 30 ]; then
            echo "    FAIL ${url} did not respond within 60s"
            "${COMPOSE[@]}" logs --tail=80
            exit 1
        fi
        sleep 2
    done
done

echo "==> Container status"
"${COMPOSE[@]}" ps

echo "==> Deploy complete. Public URLs (via Cloudflare Tunnel):"
echo "    web:   ${PUBLIC_WEB_ORIGIN:-<unset>}"
echo "    auth:  ${PUBLIC_AUTH_API_ORIGIN:-<unset>}"
echo "    store: ${PUBLIC_STORE_API_ORIGIN:-<unset>}"
