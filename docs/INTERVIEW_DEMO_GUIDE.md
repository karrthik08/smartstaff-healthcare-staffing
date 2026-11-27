# Interview demonstration guide

Use the isolated Docker stack. Do not use the development site on port 5173, and do not change the development database on port 5433.

The site is http://127.0.0.1:8080. The API is http://127.0.0.1:5081. Passwords stay in the gitignored `.env` file in the project root. They are not stored in Git.

## Startup

Compose reads `.env` from the project directory. A new terminal does not need the variables exported. Docker Desktop must be running. The script refuses to start when `.env` is missing, because a new database password will not unlock the existing volume.

```bash
./scripts/start-demo.sh
```

That validates the Compose file, starts the containers, and prints their status. It does not remove volumes.

If the stack is already running:

```bash
docker compose -f docker-compose.prod.yml ps
```

## Health checks

```bash
curl -sS http://127.0.0.1:5081/api/health
curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/
```

Expect the API to report a connected database and HTTP 200. Expect the site to return HTTP 200.

## Demo login preparation

The existing administrator is kept. A seed run adds a new facility manager, recruiter, and clinician. It does not reset older passwords and it does not delete rows.

```bash
set -a
. ./.env
set +a
export DEMO_BASE_URL=http://127.0.0.1:5081
export DEMO_ADMIN_EMAIL="$BOOTSTRAP_ADMIN_EMAIL"
export DEMO_ADMIN_PASSWORD="$BOOTSTRAP_ADMIN_PASSWORD"
export DEMO_PASSWORD="$(openssl rand -base64 18)Aa1!"
node scripts/seed-demo.mjs
```

The script refuses port 5080. Its console output lists record ids and the new email addresses. It does not print passwords. The four sign-in values are written to `.local/demo-credentials.txt` with mode `600`. That directory is gitignored.

Read them only on this machine:

```bash
chmod 600 .local/demo-credentials.txt
less .local/demo-credentials.txt
```

Sign in at http://127.0.0.1:8080/login. Sign out before switching roles.

| Role | Where to sign in | What you should see |
|---|---|---|
| Administrator | Existing bootstrap account | Administrator workspace, user list, facilities, and requisitions |
| Facility manager | New `demo-manager-...@smartstaff.invalid` account | Facility workspace, schedule, approved timesheet, and facility analytics |
| Recruiter | New `demo-recruiter-...@smartstaff.invalid` account | Recruiter workspace and the application pipeline |
| Clinician | New `demo-clinician-...@smartstaff.invalid` account | Clinician workspace, confirmed shift, attendance, and notifications |

A clinician request to create a facility returns 403. After sign-out, the previous access token is rejected.

## Navigation sequence

1. Sign in as the administrator. Open Facilities and show Demo General and Demo Unit.
2. Open the user list and show the manager and recruiter roles.
3. Open Requisitions and show the published Registered Nurse job.
4. Sign out. Sign in as the clinician. Open My profile, Applications, Schedule, and Attendance.
5. Sign out. Sign in as the recruiter. Open the application pipeline.
6. Sign out. Sign in as the facility manager. Open Scheduling, Attendance, and the approved timesheet.
7. Open Notifications, then Analytics filtered to the demo facility.
8. Sign out.

Use [docs/PRODUCT_DEMO_SCRIPT.md](PRODUCT_DEMO_SCRIPT.md) for the words. The expected API result of a fresh seed is a requisition that was published and then filled by the placement, a confirmed shift, a completed attendance record stored in UTC, an approved timesheet, notifications, and an analytics overview with at least one placement and one scheduled shift. After placement, the requisition list can show Filled rather than Published. That is the workflow completing, not a missing job.

## Role switching

Use Sign out between roles. Do not keep two sessions in one browser profile. After sign-out, the previous access token is rejected on the next API call.

## Troubleshooting

| What you see | What to do |
|---|---|
| Site does not load | Run `./scripts/start-demo.sh`. Docker Desktop must be running, and `.env` must already contain the existing database password |
| API health is not 200 | Wait for PostgreSQL to be healthy, then read `docker logs smartstaff-prod-api-1` |
| Seed script says port 5080 is refused | Point `DEMO_BASE_URL` at `http://127.0.0.1:5081` |
| Sign-in fails for a demo user | Open `.local/demo-credentials.txt` for the latest accounts. Do not reuse a password from an older seed |
| Notifications are empty | Wait about 15 seconds and refresh. The worker polls the outbox |
| You are about to type a command against port 5433 | Stop. That is the development database |

Do not run `docker compose down -v`. That would delete the isolated volume.

## Safe shutdown

This stops the containers and keeps the volumes:

```bash
docker compose -f docker-compose.prod.yml stop
```

Start them again with `./scripts/start-demo.sh`. Do not replace `POSTGRES_PASSWORD` in `.env` while this volume exists.
