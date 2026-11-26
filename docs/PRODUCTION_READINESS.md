# Production readiness

This checklist reviews the SmartStaff application as it exists in the repository. It does not mean the application has been deployed.

## Must not be used in production

| Setting | Development behavior | Production rule |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT=Development` | OpenAPI is exposed and the dev exception page can leak details | Set `Production` |
| `PASSWORD_RESET_DEV_SINK=true` | Writes reset links, including the token, to `.local/password-resets.log` | The API refuses to start in Production when this is true. Leave it unset or `false` |
| JWT signing key containing `not-for-production` | Used by automated tests | The API refuses that key in Production |
| `BOOTSTRAP_ADMIN_PASSWORD` | Creates the first administrator only when none exists | Supply it once, from a secret store, then remove it. Do not commit it |
| Empty `CORS_ORIGINS` | Falls back to local Vite and nginx origins | Production startup fails if `CORS_ORIGINS` is empty |
| `HTTPS_REDIRECT=true` inside a container behind a load balancer | Redirects the container's own HTTP port | Leave it false behind the ALB or nginx. Terminate TLS at the edge |
| Vite dev server and its `/api` proxy | Development only | Serve `frontend/dist` or the nginx image |

## Secrets

No database password, JWT signing key, or administrator password is committed. `.env` is gitignored. `.env.example` contains empty placeholders. Test signing keys live only in the test project and are rejected in Production.

## Configuration that is ready

- PostgreSQL settings come from `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, and `POSTGRES_PASSWORD`.
- JWT signing material comes from `JWT_SIGNING_KEY` and must be at least 32 bytes.
- Refresh cookies are `HttpOnly`, `SameSite=Strict`, and `Secure` when the request is HTTPS. Forwarded protocol headers are honored so a TLS-terminating proxy can make that decision.
- Auth endpoints are rate limited.
- `GET /api/health` checks PostgreSQL and returns 503 when the database is unavailable. The health payload strips messages that contain a password.
- EF Core migrations are committed and are not applied automatically at startup. There is no startup command that drops the database.
- The notification worker is a `BackgroundService`. It polls `NOTIFICATION_POLL_SECONDS` (default 2). Email delivery uses `RecordingEmailSender`, which stores messages in `EmailDispatches` and does not send them. `SesEmailSender` is not registered and throws if constructed.
- Non-development errors return a generic 500 body. Production logs use JSON on the console.

## Known authentication limitation

Logout revokes the refresh-token family. It does not revoke an access token that has already been issued. That access token remains usable until its normal expiry, which is 15 minutes unless `JWT_ACCESS_TOKEN_MINUTES` changes it. Logout does not immediately revoke all access tokens.

## Checklist before a real deployment

- [ ] Production environment variables are present and the development sink is off.
- [ ] `CORS_ORIGINS` lists only the CloudFront or application origin.
- [ ] A new JWT signing key is stored in Secrets Manager.
- [ ] RDS is private, encrypted, and backed up.
- [ ] A database backup exists before the first migration.
- [ ] The RDS engine version has been confirmed in the target region.
- [ ] An ACM certificate exists. This repository does not create one.
- [ ] GitHub Actions uses OIDC. No long-lived AWS keys are stored.
- [ ] Health checks pass after migration.
- [ ] Password reset has a real notifier. The null notifier does not send mail.
