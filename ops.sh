#!/usr/bin/env bash
# Lifecycle for svelte.umut-aslan.dev:  ./ops.sh test | deploy | logs | down
# Everything runs in containers; the host needs only Docker. CI runs the same commands.
set -euo pipefail
cd "$(dirname "$0")"

export GIT_SHA="${GIT_SHA:-$(git rev-parse --short HEAD 2>/dev/null || echo local)}"
PW_IMAGE="mcr.microsoft.com/playwright:v1.63.0-noble"
CI_PROJECT="einsatz-ci"
[ -f .env ] || { echo "missing .env (see .env.example)"; exit 1; }

backend_tests() {   # xUnit: domain rules + API against a real PostgreSQL, in the production time zone
  local net=einsatz-unit db=einsatz-unit-db
  docker network create "$net" >/dev/null 2>&1 || true
  docker rm -f "$db" >/dev/null 2>&1 || true
  docker run -d --name "$db" --network "$net" -e POSTGRES_USER=einsatz -e POSTGRES_PASSWORD=einsatz postgres:17-alpine >/dev/null
  trap 'docker rm -f einsatz-unit-db >/dev/null 2>&1 || true' RETURN
  until docker exec "$db" pg_isready -U einsatz >/dev/null 2>&1; do sleep 1; done
  DOTNET_DOCKER_ARGS="--network $net -e TZ=Europe/Berlin -e EINSATZ_TEST_DB=Host=$db;Username=einsatz;Password=einsatz" \
    ./dotnet.sh test --solution Einsatz.slnx
}

e2e_tests() {       # Playwright against the full stack (BFF + API + DB), isolated from production
  # The compose file joins Traefik's external network: it exists on the server; elsewhere (CI, a fresh clone) an
  # empty one stands in, so the stack can start (Traefik itself is off here).
  docker network inspect proxy >/dev/null 2>&1 || docker network create proxy >/dev/null
  local compose=(docker compose -p "$CI_PROJECT")
  TRAEFIK_ENABLE=false ORIGIN=http://localhost:3000 "${compose[@]}" up -d --build --wait
  trap 'docker compose -p einsatz-ci down -v >/dev/null 2>&1 || true' RETURN
  local web; web=$("${compose[@]}" ps -q web)
  docker run --rm -u "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD/e2e:/app" -w /app node:22-alpine \
    npm ci --no-audit --no-fund --ignore-scripts --silent
  docker run --rm --ipc=host --network "container:$web" -u "$(id -u):$(id -g)" -e HOME=/tmp -e CI=1 \
    -v "$PWD/e2e:/app" -w /app "$PW_IMAGE" npx playwright test
}

test()   { backend_tests; e2e_tests; }   # web unit tests + type checks run inside its image build
deploy() { test; docker compose -p einsatz up -d --build --wait --remove-orphans; docker compose -p einsatz ps; }
logs()   { docker compose -p einsatz logs -f --tail=100; }
down()   { docker compose -p einsatz down; }

"${1:-test}"
