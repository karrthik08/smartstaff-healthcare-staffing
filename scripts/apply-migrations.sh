#!/usr/bin/env sh
set -eu

# Applies committed EF Core migrations. It never drops or recreates a database.
# Refuses an obvious RDS hostname unless CONFIRM_REMOTE_MIGRATION=yes.

if [ -z "${POSTGRES_HOST:-}" ] || [ -z "${POSTGRES_PASSWORD:-}" ]; then
  echo "POSTGRES_HOST and POSTGRES_PASSWORD are required." >&2
  exit 1
fi

case "$POSTGRES_HOST" in
  *rds.amazonaws.com*|*rds.*.amazonaws.com*)
    if [ "${CONFIRM_REMOTE_MIGRATION:-}" != "yes" ]; then
      echo "Refusing to migrate a remote database without CONFIRM_REMOTE_MIGRATION=yes." >&2
      exit 1
    fi
    ;;
esac

echo "Migrating ${POSTGRES_DB:-smartstaff_db} on ${POSTGRES_HOST}:${POSTGRES_PORT:-5433}."
dotnet ef database update \
  --project backend/SmartStaff.Infrastructure/SmartStaff.Infrastructure.csproj \
  --startup-project backend/SmartStaff.Infrastructure/SmartStaff.Infrastructure.csproj
