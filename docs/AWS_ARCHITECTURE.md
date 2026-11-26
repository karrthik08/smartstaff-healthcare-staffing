# AWS architecture

The Terraform configuration in `infra/terraform` is a design. It has not been applied.

## Traffic

Browsers load the React application from CloudFront. CloudFront reads a private S3 bucket through origin access control. SPA routes return `index.html` for 403 and 404 responses.

The API sits in private subnets on ECS Fargate. Only the application load balancer security group can reach port 8080. The load balancer listens on HTTPS and forwards HTTP to the task. The container sets `HTTPS_REDIRECT=false` because TLS ends at the load balancer. Forwarded headers tell the API when the original request was HTTPS, which keeps the refresh cookie `Secure`.

## Data

RDS PostgreSQL is in the private subnets, is not publicly accessible, and has storage encryption enabled. Its security group accepts port 5432 only from the API security group. Backups are retained for 7 days and deletion protection defaults to on. A final snapshot name is set.

The database password and JWT signing key are random values written to Secrets Manager at apply time. ECS injects them into the task. Those values will exist in Terraform state, so state must later be stored in an encrypted remote backend. This repository uses local state and does not commit it.

## Identity

The task execution role can pull from ECR, write logs, and read the two secrets. The task role has no AWS permissions. The GitHub deploy role can be assumed only by `repo:OWNER/NAME:ref:refs/heads/main` through GitHub OIDC. It can push to the API repository, update the ECS service, write the frontend bucket, and create a CloudFront invalidation.

## Demonstration sizing

The default demonstration uses one NAT gateway, one small Fargate task (0.25 vCPU and 512 MB), a single-AZ `db.t4g.micro` database, 20 GB of gp3 storage, CloudFront `PriceClass_100`, and 14 days of logs. RDS PostgreSQL 18 is not assumed. `db_engine_version` defaults to 16 and must be changed to a version the region offers before apply. Local development uses PostgreSQL 18.

`acm_certificate_arn` is required. This configuration does not request a certificate or a domain.

## Monitoring

API logs go to CloudWatch log group `/ecs/smartstaff-<environment>`. The load balancer health check calls `/api/health`, which fails when PostgreSQL cannot be reached.
