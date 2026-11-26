# Environment variables

Do not commit real values. Use the shell, a gitignored `.env`, or AWS Secrets Manager.

## API

| Variable | Required | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Development` locally, `Production` for the production image and AWS |
| `POSTGRES_HOST` | Yes | `localhost` for the existing dev database on port 5433. `postgres` inside `docker-compose.prod.yml` |
| `POSTGRES_PORT` | No | Defaults to 5433 outside Compose. The production Compose database is published on `127.0.0.1:5434` and is 5432 inside the network |
| `POSTGRES_DB` | No | Defaults to `smartstaff_db` |
| `POSTGRES_USER` | No | Defaults to `smartstaff` |
| `POSTGRES_PASSWORD` | Yes | Never commit |
| `JWT_SIGNING_KEY` | Yes | At least 32 bytes. Production rejects a key marked not-for-production |
| `JWT_ISSUER` | No | Default `SmartStaff` |
| `JWT_AUDIENCE` | No | Default `SmartStaff` |
| `JWT_ACCESS_TOKEN_MINUTES` | No | Default 15 |
| `CORS_ORIGINS` | Yes in production | Comma-separated browser origins. A wildcard is rejected |
| `HTTPS_REDIRECT` | No | Set `true` only when this process itself terminates TLS |
| `COOKIE_SECURE` | No | `true` or `false`. When unset, cookies are Secure only on HTTPS requests. The local production Compose file sets `false` because it is HTTP. AWS sets `true` |
| `TRUSTED_PROXY_CIDRS` | No | Comma-separated CIDRs allowed to send `X-Forwarded-For` and `X-Forwarded-Proto`. When unset, only loopback proxies are trusted |
| `AUTH_RATE_LIMIT_PER_MINUTE` | No | Default 20 for sign-in, registration, and password endpoints |
| `API_RATE_LIMIT_PER_MINUTE` | No | Default 2000 for all requests from one address |
| `POSTGRES_MAX_POOL_SIZE` | No | Default 20 |
| `POSTGRES_MIN_POOL_SIZE` | No | Default 0 |
| `POSTGRES_CONNECT_TIMEOUT` | No | Seconds. Default 15 |
| `POSTGRES_CONNECTION_IDLE_SECONDS` | No | Default 60 |
| `NOTIFICATION_POLL_SECONDS` | No | Default 2. The production Compose file uses 15 |
| `PASSWORD_RESET_DEV_SINK` | No | `true` only on a development machine. Production startup fails if it is true |
| `BOOTSTRAP_ADMIN_EMAIL` | No | Used only when no administrator exists |
| `BOOTSTRAP_ADMIN_PASSWORD` | No | Pair with the email. Remove after the first administrator exists |
| `BOOTSTRAP_ADMIN_FIRST_NAME` | No | Optional |
| `BOOTSTRAP_ADMIN_LAST_NAME` | No | Optional |

## Frontend

| Variable | Required | Notes |
|---|---|---|
| `VITE_API_URL` | No | Leave empty when nginx or Vite proxies `/api` on the same origin. Set it only when the browser calls a different API origin. It is baked in at build time |

## Separate databases

The existing development database is the `smartstaff-postgres` container on `127.0.0.1:5433` with volume `smartstaff_pgdata`. Automated tests use database name `smartstaff_test` on that server. `docker-compose.prod.yml` creates a different Compose project and volume, `smartstaff_prod_pgdata`, published only on `127.0.0.1:5434`.
