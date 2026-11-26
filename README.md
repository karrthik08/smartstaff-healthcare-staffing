# SmartStaff

SmartStaff is a healthcare staffing application for facilities, recruiters, and clinicians. It covers accounts and roles, facilities and job requisitions, applications and placements, shift scheduling, attendance, timesheets, in-app notifications, and staffing analytics.

It is a demonstration system. It is not a HIPAA compliance claim, and it has not been deployed to AWS.

## Business problem

Hospitals and staffing teams need one place to publish openings, move candidates through review, place a clinician, schedule a shift, record attendance, and approve the hours. SmartStaff keeps that workflow in one application with role-based access.

## Features

- Sign-in, clinician self-registration, password reset, and administrator role assignment.
- JWT access tokens and refresh cookies. Logout rotates the account security stamp, so an older access token is rejected on the next API call.
- Facilities, locations, and facility membership.
- Job requisitions, recruiter assignment, and publication.
- Clinician profiles, credentials, applications, and a recruitment pipeline through placement.
- Shift creation in the facility timezone, publication, assignment, and clinician confirmation.
- Attendance stored in UTC. The clock form sends the facility's local time, and the API converts it. A missing or repeated daylight-saving hour is rejected. Older attendance rows are not rewritten.
- Timesheets, submission, and manager approval. Timesheet hour fields are still entered as UTC.
- In-app notifications with a bell, a notification page, and preferences. Email is recorded in the database. Amazon SES is not connected.
- Analytics for recruitment, scheduling, attendance, timesheets, and credentials, plus CSV export.
- An administrator user list, account activation, and an audit log.

## Technology

- ASP.NET Core 10 and C#
- React, TypeScript, and Material UI
- PostgreSQL and Entity Framework Core
- Docker Compose
- JWT authentication and ASP.NET Core Identity
- Terraform and GitHub Actions definitions that have not been executed against AWS
- NUnit, Jest, React Testing Library, and axe-core through jest-axe
- Recharts

## Architecture

The React client calls the API through `/api`. In local development, Vite proxies that path to the API. In the production-style Compose file, nginx does the same.

The API is the only process that talks to PostgreSQL. Application services live in the Infrastructure project and return operation results to the controllers. Identity, staffing, recruitment, scheduling, timekeeping, notifications, and analytics are separate modules on one database.

See `docs/ARCHITECTURE.md`.

## Local setup

Prerequisites: .NET SDK 10, Node.js 22, and Docker. The development database is the existing container `smartstaff-postgres` on port 5433, database `smartstaff_db`. Do not delete that container or its volume `smartstaff_pgdata`.

```bash
dotnet restore SmartStaff.sln
dotnet tool restore
cd frontend && npm ci
```

Export the database password from the container. Do not copy it into a file.

```bash
export POSTGRES_HOST=localhost
export POSTGRES_PORT=5433
export POSTGRES_DB=smartstaff_db
export POSTGRES_USER=smartstaff
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
export PASSWORD_RESET_DEV_SINK=true
```

```bash
dotnet run --project backend/SmartStaff.API/SmartStaff.API.csproj --launch-profile http
```

```bash
cd frontend && npm run dev -- --host 127.0.0.1 --port 5173 --strictPort
```

| App | URL |
|---|---|
| Frontend | http://127.0.0.1:5173 |
| API | http://127.0.0.1:5080 |
| Health | http://127.0.0.1:5080/api/health |

Do not set `BOOTSTRAP_ADMIN_EMAIL` against this database. An administrator already exists.

## Docker

`docker-compose.yml` is the development database definition and points at the existing data volume. `docker-compose.prod.yml` is a separate stack. Its volume is `smartstaff_prod_pgdata`, PostgreSQL is published on `127.0.0.1:5434`, the API on `127.0.0.1:5081`, and the site on `http://127.0.0.1:8080`.

```bash
export POSTGRES_PASSWORD="$(openssl rand -base64 24)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
docker compose -f docker-compose.prod.yml up -d postgres
POSTGRES_HOST=127.0.0.1 POSTGRES_PORT=5434 POSTGRES_DB=smartstaff_db POSTGRES_USER=smartstaff ./scripts/apply-migrations.sh
docker compose -f docker-compose.prod.yml up -d --build
```

Keep those secrets in the shell or an untracked env file. Do not commit them.

## Database migrations

Migrations are additive files under `backend/SmartStaff.Infrastructure/Persistence/Migrations`. Apply them with:

```bash
./scripts/apply-migrations.sh
```

The script runs `dotnet ef database update`. It refuses a hostname containing `rds.amazonaws.com` unless `CONFIRM_REMOTE_MIGRATION=yes`. Do not drop the database, and do not run a migration `Down`.

## Testing

```bash
dotnet test SmartStaff.sln -c Release
cd frontend && npm test && npx tsc -b --pretty false && npm run lint && npm run build
```

Details are in `docs/TESTING_GUIDE.md`.

## Screenshots

No screenshots are stored in this repository.

## Deployment status

AWS resources have not been created. Terraform has been validated locally and has not been applied. GitHub Actions workflow files exist and have not been used to deploy. See `docs/DEPLOYMENT_CHECKLIST.md`.

## Known limitations

See `docs/KNOWN_LIMITATIONS.md`. Automated accessibility checks do not prove WCAG conformance. Email does not leave the application. The cloud database default in Terraform is PostgreSQL 16, while local Docker uses PostgreSQL 18.
