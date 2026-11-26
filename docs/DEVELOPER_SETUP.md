# Developer setup

## Tools

- .NET SDK 10
- Node.js 22
- Docker Desktop
- The existing PostgreSQL 18 container `smartstaff-postgres`

That container publishes `localhost:5433` and uses the volume `smartstaff_pgdata`. Do not run `docker compose down -v` against it, and do not drop `smartstaff_db`.

## Restore

```bash
dotnet restore SmartStaff.sln
dotnet tool restore
cd frontend && npm ci
```

`dotnet-ef` is a local tool. Migration commands use `--project backend/SmartStaff.Infrastructure --startup-project backend/SmartStaff.Infrastructure`.

## Run

```bash
export POSTGRES_HOST=localhost POSTGRES_PORT=5433 POSTGRES_DB=smartstaff_db POSTGRES_USER=smartstaff
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
export PASSWORD_RESET_DEV_SINK=true
dotnet run --project backend/SmartStaff.API/SmartStaff.API.csproj --launch-profile http
```

Restarting the API with a new signing key invalidates access tokens. Refresh cookies remain valid until logout or expiry. Do not set bootstrap administrator variables when this database already has an administrator.

```bash
cd frontend && npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

`frontend/.env.development` leaves `VITE_API_URL` empty so the browser stays on the Vite origin and the proxy forwards `/api` to `http://127.0.0.1:5080`.

## Migrations

Inspect `__EFMigrationsHistory` before applying a migration. Take a custom-format `pg_dump` and check it with `pg_restore -l`. Then run `scripts/apply-migrations.sh` with `POSTGRES_PORT=5433` for development or `5434` for the isolated stack. Confirm the migration `Up` method before you apply it. Stop if it drops or rewrites existing data.

## Tests

NUnit reads `POSTGRES_HOST`, `POSTGRES_PORT`, and `POSTGRES_PASSWORD`, then switches the database name to `smartstaff_test`. Set the port to 5433 explicitly. A shell left on 5434 will point tests at the isolated database.
