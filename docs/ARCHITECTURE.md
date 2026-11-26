# Architecture

SmartStaff is one API and one React client sharing one PostgreSQL database.

## Projects

| Project | Responsibility |
|---|---|
| `SmartStaff.API` | HTTP, authentication middleware, cookies, rate limits, and controllers |
| `SmartStaff.Application` | Request and response contracts and validation |
| `SmartStaff.Domain` | Facility, requisition, recruitment, shift, attendance, and timesheet types |
| `SmartStaff.Infrastructure` | EF Core, Identity, and the service implementations |
| `SmartStaff.Tests` | NUnit tests against a migrated `smartstaff_test` database |
| `frontend` | React routes, Material UI, and the API client |

## Request path

The browser sends `/api` to Vite in development or to nginx in the production-style image. The API validates the caller, calls an application service, and maps the result to an HTTP status. Create operations that insert a new row return 201. A cross-facility read of an existing record returns 403. A missing record returns 404.

## Authentication

Access tokens are JWTs and include the Identity security stamp in the `sst` claim. Each API request compares that claim with PostgreSQL. Logout, a password change, a role change, and deactivation rotate the stamp, so previously issued access tokens fail on the next request. Refresh tokens are stored hashed and are sent in an HTTP-only cookie. Cookie `Secure` follows `COOKIE_SECURE`, or the request scheme when that variable is unset. `TRUSTED_PROXY_CIDRS` limits which proxy addresses may supply forwarded headers.

## Time

Shift times are entered in the facility timezone and stored as UTC. Clock-in and clock-out accept either an offset in `at` or a facility-local value in `local`. A local value that does not exist, or that occurs twice because of daylight saving, is rejected. Stored attendance is not rewritten when the conversion rules change. Timesheet entry times are still sent with a UTC offset.

## Notifications

Domain actions write an outbox row. A hosted service polls that outbox and records an in-app notification. Delivery stops after eight attempts. The registered email sender writes a row to `EmailDispatches`. `SesEmailSender` exists and is not registered.

## Isolated production-style stack

`docker-compose.prod.yml` builds the API and the nginx frontend, uses its own PostgreSQL volume, and does not attach to the development volume `smartstaff_pgdata`.
