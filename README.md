# SmartStaff

SmartStaff is a healthcare staffing management application. Phase 1 provides the ASP.NET Core API, the React client, and a health check that reads a live PostgreSQL connection.

## Prerequisites

- macOS on Apple silicon, or another host with the same tools
- .NET SDK 10.0.401
- Node.js 22 and npm
- Docker Desktop with Compose v2
- The existing PostgreSQL 18 container `smartstaff-postgres`

That container is already running and owns the persistent volume `smartstaff_pgdata`. Do not delete or recreate it.

| Setting | Value |
|---|---|
| Container | `smartstaff-postgres` |
| Image | `postgres:18` |
| Host | `localhost` |
| Port | `5433` |
| Database | `smartstaff_db` |
| Username | `smartstaff` |

The database password stays in the container environment. Export it in your shell when a command needs it. Do not write it into source files or commit it.

```bash
export POSTGRES_HOST=localhost
export POSTGRES_PORT=5433
export POSTGRES_DB=smartstaff_db
export POSTGRES_USER=smartstaff
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
```

## Installation

From the repository root:

```bash
dotnet restore SmartStaff.sln
dotnet tool restore
cd frontend
npm ci
```

`dotnet-ef` 10.0.15 is installed as a local tool from `dotnet-tools.json`.

## Run the API and the React app

Start the API:

```bash
dotnet run --project backend/SmartStaff.API/SmartStaff.API.csproj --launch-profile http
```

Start the frontend in another terminal:

```bash
cd frontend
npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

| App | URL |
|---|---|
| Frontend | http://127.0.0.1:5173 |
| API | http://localhost:5080 |
| Health | http://localhost:5080/api/health |

The system-status page calls `GET /api/health`. A healthy database returns HTTP 200 with `"database": "Connected"`. If PostgreSQL cannot be reached, the API returns HTTP 503 and the page shows that failure. The page does not invent staffing data.

The Vite dev server proxies `/api` to `http://localhost:5080`. `frontend/.env.development` leaves `VITE_API_URL` empty so the browser calls the Vite origin. Refresh and CSRF cookies stay on that origin.

## Authentication

Generate a signing key in the shell that starts the API. Do not write it into a committed file.

```bash
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
export PASSWORD_RESET_DEV_SINK=true
```

`JWT_SIGNING_KEY` must be at least 32 bytes. Public registration creates clinician accounts only. Administrator, staffing coordinator, and hospital administrator roles are assigned through `PUT /api/admin/users/{id}/roles` by an administrator. Access tokens stay in memory. The refresh token is an HttpOnly cookie named `smartstaff.refresh`, and state-changing refresh and logout requests must send the `smartstaff.csrf` cookie value in `X-CSRF-TOKEN`.

With `PASSWORD_RESET_DEV_SINK=true`, reset links are appended to `.local/password-resets.log` under the API working directory. That directory is gitignored. The API response never includes the reset token.

## Database migrations

The initial migration is `20261126015401_InitialCreate`. Apply it only against the existing database:

```bash
dotnet ef database update \
  --project backend/SmartStaff.Infrastructure/SmartStaff.Infrastructure.csproj \
  --startup-project backend/SmartStaff.Infrastructure/SmartStaff.Infrastructure.csproj
```

Confirm the history table:

```bash
docker exec smartstaff-postgres \
  psql -U smartstaff -d smartstaff_db \
  -c 'SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory";'
```

## Tests

Backend:

```bash
dotnet test SmartStaff.sln
```

The health integration test calls the real database. It runs when `POSTGRES_PASSWORD` is set.

Frontend:

```bash
cd frontend
npm test
npm run build
```

## Docker Compose

Compose builds the API and the nginx frontend. It connects the API to `smartstaff-postgres` through the host port `5433`. It does not declare a database service.

Stop the local `dotnet run` and `npm run dev` processes first, because Compose publishes the API on port 5080.

```bash
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
docker compose build
docker compose up
```

| Service | URL |
|---|---|
| API | http://localhost:5080/api/health |
| Frontend | http://localhost:8080 |

Nginx proxies `/api` to the API container, so the page on port 8080 uses same-origin cookies. `CORS_ORIGINS` still includes the local development origins.

Stop Compose with `docker compose down`. Do not add `-v`, and do not remove `smartstaff-postgres` or `smartstaff_pgdata`.

## Configuration

Copy `.env.example` to `.env` for Compose, or export the same variables. Leave `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY` empty in the example. Export a real signing key before `docker compose up`. The API refuses to start without one.

## Troubleshooting

**The API exits with `POSTGRES_PASSWORD environment variable is required`.** Export the password from the running container, as shown above. Restart the API after exporting it.

**Health returns 503 or the dashboard says the API could not be reached.** Confirm `smartstaff-postgres` is running and that `127.0.0.1:5433` is published. Open the frontend at `http://127.0.0.1:5173` or `http://localhost:5173`. Both origins are allowed. A different host port needs to be added to `CORS_ORIGINS`.

**Port 5080 or 5173 is already in use.** Stop the existing SmartStaff process, or choose another port and update `VITE_API_URL` and `CORS_ORIGINS` to match.

**`dotnet ef` cannot see the database.** Run `dotnet tool restore`, confirm `POSTGRES_PORT` is `5433`, and confirm the password variable is set in that same shell.

**Docker build cannot reach PostgreSQL.** The API container uses `host.docker.internal` and port `5433`, which is the published host port. Do not point it at port `5432` on the host. Do not replace the existing database container.

**npm reports moderate `glob` or `minimatch` issues.** Those come from Jest's development dependencies. `frontend/package.json` overrides them with `glob` 14.0.6 and `minimatch` 11.0.9. Do not downgrade Jest to 29 to clear the audit. That downgrade is a major version change.
