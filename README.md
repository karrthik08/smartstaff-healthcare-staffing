# SmartStaff

Independent proof of concept for healthcare staffing operations.

SmartStaff is not an official product of any staffing company. No employer requested, approved, sponsored, or supplied materials for it. It explores a common staffing workflow with a working full-stack application.

## Executive summary

Staffing teams coordinate facilities, recruiters, and clinicians across requisitions, interviews, placements, shifts, attendance, and timesheets. Those steps are often split across inboxes and spreadsheets. SmartStaff puts the same path in one role-based application: publish a job, move a candidate to placement, schedule a shift, record attendance, approve a timesheet, and review the result.

The application runs locally with Docker. It has not been deployed to a public host, and it is not a production or HIPAA claim.

## Business problem

A facility needs a clinician for a specific unit and shift. A recruiter has to find a qualified person, the facility has to decide, and someone has to confirm the schedule and the hours. When each handoff lives in a different tool, status is hard to see, two shifts can overlap, and timesheets wait on email.

SmartStaff is a proposed way to keep that handoff in one system. It does not assert that any particular company has these gaps, and it does not claim measured savings.

## Proposed solution

Four roles share one system:

- An administrator creates the facility and assigns responsibilities.
- A facility manager reviews candidates, schedules shifts, and approves hours.
- A recruiter moves applications and creates the placement.
- A clinician maintains a profile, applies, confirms a shift, and submits time.

The React client talks to an ASP.NET Core API. The API is the only component that writes to PostgreSQL.

## Technology stack

| Area | Choice |
|---|---|
| API | ASP.NET Core 10, C# |
| Client | React, TypeScript, Material UI, Vite |
| Data | PostgreSQL 18, Entity Framework Core |
| Identity | ASP.NET Core Identity, JWT access tokens, refresh cookies |
| Runtime | Docker Compose |
| Tests | NUnit, Jest, React Testing Library, axe-core |
| Charts | Recharts |

Terraform and a GitHub Actions deploy workflow describe a possible future AWS layout. They have not been applied. The demonstration does not depend on them.

## System architecture

```text
Browser
  React + TypeScript
        |
        |  /api
        v
ASP.NET Core
  JWT authentication
  Role-based authorization
  Staffing, recruitment, scheduling, timekeeping
  Notification worker and transactional outbox
  Audit log and analytics
        |
        v
PostgreSQL
```

Details and tradeoffs are in [docs/SYSTEM_ARCHITECTURE.md](docs/SYSTEM_ARCHITECTURE.md).

## Core features

- Accounts, clinician self-registration, password reset, and administrator role assignment.
- Facilities, locations, and facility membership.
- Job requisitions, recruiter assignment, and publication.
- Clinician profiles, credentials, applications, and a status pipeline through placement.
- Shifts in the facility timezone, assignment, and clinician confirmation.
- Attendance stored in UTC. The clock form accepts facility local time and rejects an ambiguous or missing daylight-saving hour.
- Timesheet submission and manager approval. Timesheet hour entry is still UTC.
- In-app notifications. Email is stored in the database and is not sent through an external provider.
- Analytics and CSV export for the caller’s authorized facilities.
- An audit log of security and workflow actions.

## User roles

| Role | What they can do |
|---|---|
| Administrator | Manage users, facilities, requisitions, and organization analytics |
| Facility manager | Review facility applications, schedule shifts, approve timesheets |
| Recruiter | Advance assigned applications and create placements |
| Clinician | Maintain a profile, apply, confirm shifts, record attendance, submit time |

Public registration creates a clinician only. Other roles are assigned by an administrator.

## Application workflow

1. The administrator creates a facility and location, assigns a facility manager, and publishes a requisition to an assigned recruiter.
2. The clinician saves a profile and applies.
3. The recruiter moves the application through review and submission to the facility.
4. The facility manager records the interview and offer.
5. The recruiter creates the placement.
6. The facility manager publishes a shift and assigns the clinician. The clinician confirms it.
7. The clinician clocks in and out in facility local time. The API stores UTC.
8. The clinician submits a timesheet. The facility manager approves it.
9. Notifications record the handoffs. Analytics reads the same database.

## Security design

Access tokens are short-lived JWTs. The token carries the Identity security stamp. Each API request compares that stamp with PostgreSQL, so logout, password changes, role changes, and deactivation reject older tokens. Refresh tokens are hashed and stored in an HTTP-only cookie. Refresh and logout also require a CSRF header that matches a non-HTTP-only cookie.

Authorization is role-based, and facility data is further limited to facilities the caller may manage or recruit for. A cross-facility read of an existing record returns 403. A missing record returns 404. Authentication and other API routes are rate limited. Audit rows record the action and whether it succeeded, not the password.

## Database design

EF Core migrations create the schema. Identity tables sit beside facilities, requisitions, profiles, applications, placements, shifts, attendance, timesheets, a notification outbox, and audit logs. Attendance timestamps are stored as offsets. The latest migration adds only the data-protection key table.

The development database on port 5433 is separate from the isolated demonstration database on port 5434. Tests use `smartstaff_test` and do not drop the development database. See [docs/DATABASE_SCHEMA.md](docs/DATABASE_SCHEMA.md).

## Docker setup

`docker-compose.prod.yml` is the demonstration stack. It has its own volume, `smartstaff_prod_pgdata`, and does not attach to the development volume.

```bash
export POSTGRES_PASSWORD="$(openssl rand -base64 24)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
export BOOTSTRAP_ADMIN_EMAIL="admin@smartstaff.invalid"
export BOOTSTRAP_ADMIN_PASSWORD="$(openssl rand -base64 18)Aa1!"
export BOOTSTRAP_ADMIN_FIRST_NAME="Avery"
export BOOTSTRAP_ADMIN_LAST_NAME="Admin"
docker compose -f docker-compose.prod.yml up -d postgres
POSTGRES_HOST=127.0.0.1 POSTGRES_PORT=5434 POSTGRES_DB=smartstaff_db POSTGRES_USER=smartstaff ./scripts/apply-migrations.sh
docker compose -f docker-compose.prod.yml up -d --build
```

Keep those values in the shell or an untracked env file. Do not commit them.

| Service | URL |
|---|---|
| Site | http://127.0.0.1:8080 |
| API health | http://127.0.0.1:5081/api/health |

`docker-compose.yml` is the development database definition. Do not remove its volume.

## Local demonstration

[docs/INTERVIEW_DEMO_GUIDE.md](docs/INTERVIEW_DEMO_GUIDE.md) has startup, health, and shutdown steps. [docs/PRODUCT_DEMO_SCRIPT.md](docs/PRODUCT_DEMO_SCRIPT.md) has a 3-minute conversation and a 10-minute technical walkthrough. `scripts/seed-demo.mjs` creates fictional accounts on the isolated API and refuses the development API on port 5080.

## Testing and validation

```bash
export POSTGRES_HOST=localhost POSTGRES_PORT=5433 POSTGRES_USER=smartstaff
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
dotnet test SmartStaff.sln -c Release
cd frontend && npm test && npx tsc -b --pretty false && npm run lint && npm run build
```

NUnit covers authentication, staffing, recruitment, scheduling, timekeeping, notifications, and analytics against a migrated test database. Jest covers the main screens, including an axe-core pass. That automated pass does not prove WCAG conformance. See [docs/TESTING_GUIDE.md](docs/TESTING_GUIDE.md).

## Screenshots

Captured screens from the local demonstration are listed in [docs/DEMO_ASSETS.md](docs/DEMO_ASSETS.md). They use fictional accounts. No passwords are shown.

## Known limitations

- The application has not been deployed, penetration tested, or assessed for HIPAA or production use.
- A stolen access token remains valid until it expires or the security stamp changes. Logout does change the stamp.
- Timesheet hour fields are entered as UTC. Clock-in and clock-out use facility local time.
- Email is stored in `EmailDispatches`. No external mail provider is connected.
- Some maintenance forms still ask for record identifiers. The seeded demonstration is the path used for a walkthrough.
- Screen-reader testing has not been completed.

More detail is in [docs/KNOWN_LIMITATIONS.md](docs/KNOWN_LIMITATIONS.md).

## Future enhancements

A later evaluation could add external email, single sign-on, richer calendar views, credential-document storage, and a hosted environment. Those are not implemented. A possible AWS shape is documented separately and has not been provisioned. Any enterprise integration would need its own security review, and this repository does not claim that review has been done.
