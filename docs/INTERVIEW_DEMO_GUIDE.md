# Interview demonstration guide

Use the isolated Docker stack. Do not use the development site on port 5173, and do not change the development database on port 5433.

The site is http://127.0.0.1:8080. The API is http://127.0.0.1:5081. Passwords stay in the shell. They are not stored in this repository.

## Startup

From the repository root, if the stack is already running, do not recreate it:

```bash
docker compose -f docker-compose.prod.yml ps
```

If it is not running, start it with the same untracked env file you used before. Do not invent a new database password for an existing volume.

```bash
docker compose -f docker-compose.prod.yml --env-file /path/to/untracked.env up -d
```

The env file must supply `POSTGRES_PASSWORD` and `JWT_SIGNING_KEY`. Add bootstrap administrator values only when the isolated database does not already have an administrator.

## Health checks

```bash
curl -sS http://127.0.0.1:5081/api/health
curl -sS -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/
```

Expect the API to report a connected database and HTTP 200. Expect the site to return HTTP 200.

## Demo login preparation

Generate a demonstration password in the shell. It must be at least 12 characters and include upper and lower case, a number, and a symbol. Do not write it into a file in the repository.

```bash
export DEMO_BASE_URL=http://127.0.0.1:5081
export DEMO_ADMIN_EMAIL="the isolated bootstrap email"
export DEMO_ADMIN_PASSWORD="the isolated bootstrap password"
export DEMO_PASSWORD="$(openssl rand -base64 18)Aa1!"
node scripts/seed-demo.mjs
```

The script refuses port 5080. It prints fictional email addresses and record ids. It does not print passwords. Each run adds a new facility and new accounts. It does not delete earlier rows.

Sign in at http://127.0.0.1:8080/login.

| Role | How to identify the account |
|---|---|
| Administrator | `DEMO_ADMIN_EMAIL` |
| Facility manager | `demo-manager-<timestamp>@smartstaff.invalid` |
| Recruiter | `demo-recruiter-<timestamp>@smartstaff.invalid` |
| Clinician | `demo-clinician-<timestamp>@smartstaff.invalid` |

The manager, recruiter, and clinician share `DEMO_PASSWORD` for that run only. The administrator uses the existing bootstrap password.

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
| Site does not load | Run `docker compose -f docker-compose.prod.yml ps` and start the stack with the existing env file |
| API health is not 200 | Wait for PostgreSQL to be healthy, then read `docker logs smartstaff-prod-api-1` |
| Seed script says port 5080 is refused | Point `DEMO_BASE_URL` at `http://127.0.0.1:5081` |
| Sign-in fails for a demo user | Run the seed again. A new run uses new emails. Do not reuse a password from an earlier run |
| Notifications are empty | Wait about 15 seconds and refresh. The worker polls the outbox |
| You are about to type a command against port 5433 | Stop. That is the development database |

Do not run `docker compose down -v`. That would delete the isolated volume.

## Safe shutdown

This stops the containers and keeps the volumes:

```bash
docker compose -f docker-compose.prod.yml stop
```

Start them again with `docker compose -f docker-compose.prod.yml --env-file /path/to/untracked.env up -d`.
