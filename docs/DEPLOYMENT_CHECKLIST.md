# Deployment checklist

Nothing in this checklist has been executed against AWS. Do not run `terraform apply` until the blockers below are resolved and a deployment is explicitly approved.

## Already in the repository

- `infra/terraform` describes a VPC, one NAT gateway by default, private ECS Fargate, an HTTPS load balancer, private RDS, CloudFront, S3, CloudWatch logs, Secrets Manager, and a GitHub OIDC deploy role.
- `infra/terraform/backend.s3.hcl.example` is an example only. The active backend is local. State is not remote.
- `.github/workflows/ci.yml` runs on pull requests. `.github/workflows/deploy.yml` is `workflow_dispatch` only and uses OIDC. It has not been run.
- `GET /api/health` is the load balancer health check and fails when PostgreSQL is unreachable.
- RDS backup retention in the configuration is 7 days, deletion protection defaults to on, and a final snapshot name is set. Recovery is restore-forward. Do not use EF `Down`.
- API logs are configured for CloudWatch group `/ecs/smartstaff-<environment>`.
- Local cost notes are formulas in `docs/COST_ESTIMATE.md`. NAT and the load balancer dominate an idle bill. No dollar amount is claimed here.

## Prerequisites before any apply

1. An AWS account and a region where the chosen RDS PostgreSQL version exists. The variable `db_engine_version` defaults to `16`. Local development and Compose use PostgreSQL 18. Confirm the regional version before changing it.
2. An existing ACM certificate in that region. `acm_certificate_arn` has no default. This Terraform does not request a certificate.
3. DNS that points the API name at the load balancer and, if used, the frontend name at CloudFront. `frontend_domain_name` is empty until that exists. `cors_origins` is required and must match the real site origin.
4. A GitHub repository name in `owner/name` form for `github_repository`. The OIDC role trusts `refs/heads/main` only. Confirm the GitHub OIDC thumbprint in `iam.tf` is still current.
5. An encrypted remote Terraform backend, replacing the example `backend.s3.hcl`. Do not apply with only local state if the state will hold resource identifiers.
6. After apply, put the JWT value into the Secrets Manager secret with `put-secret-value`. Terraform creates the secret and does not write the secret value.
7. RDS uses `manage_master_user_password`. The master password is in the RDS-managed secret, not in a Terraform variable. The task reads that secret.
8. Run `scripts/apply-migrations.sh` against the new database only after a snapshot and only with `CONFIRM_REMOTE_MIGRATION=yes`. Do not point it at the development database.

## Rollback

Keep the previous container image. If a migration has been applied, restore the database snapshot taken before that migration and deploy the application build that matches it. Redeploying an older image onto a newer schema is not a tested rollback.

## Not configured

- SES is not in Terraform. `SesEmailSender` is not the registered sender. Notifications record email in `EmailDispatches`.
- No WAF, no multi-AZ database, and no production paging alarms are defined.
- GitHub environment secrets for a live deploy are not stored in this repository and were not created from here.

## Current blockers

- No AWS account resources have been authorized.
- Remote state is not configured.
- ACM certificate and DNS names are not supplied.
- RDS PostgreSQL 18 availability in the target region was not verified, so the default remains 16.
- The JWT secret has no value until someone writes it after apply.
- SES is not integrated.
- This application has not been assessed for HIPAA or production readiness.
