# Database recovery

## What was verified

`dotnet ef database update` completed on ephemeral PostgreSQL 16 and PostgreSQL 18 containers. Those containers were removed afterward. They did not use `smartstaff_pgdata`.

The same migration set, including the additive `DataProtectionKeys` table, was applied to the isolated database on `127.0.0.1:5434`. It was not applied to `smartstaff_db` on `127.0.0.1:5433`. After the tests, that development database still had 8 users, 1 facility, 1 requisition, 1 placement, 2 shift assignments, 3 shifts, and 2 timesheets. `DataProtectionKeys` was absent there.

A custom-format dump of the isolated database restored into a new database named `smartstaff_restore` on that same isolated server. The source and restore each had 7 users. The restore had the approved Phase 11 timesheet. `smartstaff_restore` was then dropped. The isolated `smartstaff_db` and the development database were not dropped.

`scripts/apply-migrations.sh` still refuses an RDS host unless `CONFIRM_REMOTE_MIGRATION=yes`. It only runs `dotnet ef database update`.

## Backup

From the database container, write a custom-format dump:

```sh
docker exec smartstaff-postgres pg_dump -U smartstaff -d smartstaff_db -Fc -f /tmp/smartstaff.dump
docker cp smartstaff-postgres:/tmp/smartstaff.dump ./smartstaff.dump
```

Use the isolated container name `smartstaff-prod-postgres-1` when backing up that stack. Copy the dump out of the container and store it outside the database volume. Do not commit it.

## Restore

Restore into a new database first. Confirm row counts. Only then consider replacing a database, and only with an explicit decision to discard that database.

```sh
docker exec smartstaff-postgres psql -U smartstaff -d postgres -c 'CREATE DATABASE smartstaff_restore;'
docker cp ./smartstaff.dump smartstaff-postgres:/tmp/smartstaff.dump
docker exec smartstaff-postgres pg_restore -U smartstaff -d smartstaff_restore /tmp/smartstaff.dump
```

Compare `Users`, `Timesheets`, and `AuditLogs` before dropping anything. Dropping `smartstaff_restore` does not affect `smartstaff_db`. Do not use `docker compose down -v`.

## Rollback

EF migrations in this repository are forward-only for operations. Do not run `dotnet ef database update` to an older migration on a database that holds approved timesheets. Restore the dump taken before the migration instead. The Phase 11 migration creates `DataProtectionKeys` and does not alter timesheet or audit rows. Applying it to the development database is still a schema change and was intentionally skipped.

Connection pooling defaults to a maximum of 20, a minimum of 0, a 15-second connect timeout, and a 60-second idle lifetime. Override them with `POSTGRES_MAX_POOL_SIZE`, `POSTGRES_MIN_POOL_SIZE`, `POSTGRES_CONNECT_TIMEOUT`, and `POSTGRES_CONNECTION_IDLE_SECONDS`.
