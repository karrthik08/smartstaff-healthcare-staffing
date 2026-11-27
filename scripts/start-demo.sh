#!/bin/sh
set -eu
cd "$(dirname "$0")/.."

if [ ! -f .env ]; then
  echo "Missing .env. This stack needs the existing PostgreSQL password and a JWT signing key." >&2
  echo "Do not invent a new database password for the existing volume." >&2
  exit 1
fi

docker compose -f docker-compose.prod.yml config --quiet
docker compose -f docker-compose.prod.yml up -d

ready=0
i=1
while [ "$i" -le 15 ]; do
  if curl -fsS http://127.0.0.1:5081/api/health >/dev/null; then
    ready=1
    break
  fi
  sleep 2
  i=$((i + 1))
done

if [ "$ready" != 1 ]; then
  echo "API health check did not succeed." >&2
  docker compose -f docker-compose.prod.yml ps
  exit 1
fi

web=$(curl -sS -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/ || true)
echo "Frontend: http://127.0.0.1:8080 (HTTP ${web})"
echo "API health: http://127.0.0.1:5081/api/health"
docker compose -f docker-compose.prod.yml ps
