# Deployment

Nothing in this repository creates AWS resources or deploys the application. Run these steps only after an explicit decision to deploy.

## Prerequisites

- .NET 10 SDK and Node.js 22 for local checks.
- Docker for image builds.
- Terraform 1.6 or newer, without running apply until AWS access is approved.
- An ACM certificate in the chosen region if the load balancer should serve HTTPS.
- A GitHub repository setting `AWS_ROLE_ARN` and the other Actions variables listed in `docs/CI_CD.md`.
- OIDC trust from GitHub to the deploy role created by Terraform. Do not create an IAM access key.

## Local production-style stack

`docker-compose.yml` remains the development file and still points at the existing database on the host. `docker-compose.prod.yml` is separate. It stores data in volume `smartstaff_prod_pgdata`, mounted at `/var/lib/postgresql` so the PostgreSQL 18 image can keep its versioned data directory, and publishes PostgreSQL on `127.0.0.1:5434`. It does not mount `smartstaff_pgdata`.

```sh
export POSTGRES_PASSWORD="$(openssl rand -base64 24)"
export JWT_SIGNING_KEY="$(openssl rand -base64 48)"
docker compose -f docker-compose.prod.yml up -d postgres
POSTGRES_HOST=127.0.0.1 POSTGRES_PORT=5434 POSTGRES_DB=smartstaff_db POSTGRES_USER=smartstaff \
  ./scripts/apply-migrations.sh
docker compose -f docker-compose.prod.yml up -d --build
```

The site is `http://127.0.0.1:8080`. The API health URL from the host is `http://127.0.0.1:5081/api/health`. Nginx also proxies `/api` on port 8080.

Do not point `scripts/apply-migrations.sh` at an RDS hostname unless `CONFIRM_REMOTE_MIGRATION=yes`. The script runs `dotnet ef database update` only. It does not drop databases.

## Database backup and restore

Before any production migration:

1. Create an RDS snapshot, or run `pg_dump` against the target and store the dump outside the database.
2. Record the current migration name from `__EFMigrationsHistory`.
3. Run `scripts/apply-migrations.sh` as its own step, before new tasks start serving traffic.
4. Check `GET /api/health`.

Restore by replacing the database from the snapshot or from `pg_restore`, then deploy the application build that matches that migration. EF Core down migrations are not part of the deployment workflow because several historical migrations drop tables in `Down`. Rolling forward with a restore is the recovery path.

## AWS shape

Terraform in `infra/terraform` describes the target. Apply it only with approved credentials. After the first image exists in ECR, run the manually triggered Deploy workflow. That workflow assumes an IAM role with GitHub OIDC, pushes the API image, forces an ECS deployment, syncs the frontend to S3, invalidates CloudFront, and fails if the health URL is not 200.

## Rollback

1. Restore the database snapshot if a migration was applied and must be undone.
2. Point the ECS service at the previous image tag and force a new deployment.
3. Sync the previous frontend build to S3 and invalidate CloudFront.
4. Confirm `/api/health` returns 200.
