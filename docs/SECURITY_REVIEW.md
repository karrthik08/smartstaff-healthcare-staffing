# Security review

Phase 11 reviewed the existing authentication, authorization, and deployment configuration. The results below are from the local test database, the isolated Compose stack, and `terraform validate`. Nothing was deployed.

## Findings and fixes

Logout used to revoke only the refresh-token family. A copied access token kept working until it expired. Logout, role changes, and deactivation now rotate the ASP.NET Core Identity security stamp. Password changes already rotated it. Each access token carries the stamp, and every API instance compares it with PostgreSQL on the request. A token from before the change is rejected. This works across multiple containers because the stamp is a database value, not process memory.

Data-protection keys are stored in the `DataProtectionKeys` table. Password-reset tokens can survive an API restart and are the same on every instance that shares the database. The migration only creates that table. It was applied to `smartstaff_test` and to the isolated database on port 5434. It was not applied to the development database `smartstaff_db` on port 5433.

Forwarded headers are trusted only for CIDRs in `TRUSTED_PROXY_CIDRS`. When that variable is empty, the framework default remains loopback. Production cookies are Secure when `COOKIE_SECURE=true`. The local HTTP Compose stack sets it to false so browsers can still store the cookie there. AWS task configuration sets it to true and trusts the VPC CIDR.

CORS rejects a wildcard origin. Production still requires an explicit origin list, rejects the development password-reset sink, and rejects a development JWT signing key. Auth endpoints stay limited to 20 requests a minute per address. All requests share a separate limit of 2,000 a minute unless `API_RATE_LIMIT_PER_MINUTE` overrides it. API errors outside Development stay generic. Notification logs record an exception type, not the exception text.

Facility isolation and role checks were exercised again. A clinician received 403 for facility creation, administrator dashboard access was already covered by NUnit, and a clinician received 403 when trying to approve a timesheet.

## Terraform and GitHub Actions

Terraform no longer generates database or JWT passwords, so a later apply would not write those values into state. RDS manages the master password in Secrets Manager. The JWT secret is only a container; the value has to be added with `put-secret-value` after apply. The execution role can read those two secrets. The task role still has no AWS permissions.

`infra/terraform/backend.s3.hcl.example` shows an encrypted S3 backend and a DynamoDB lock table. It is not active. `terraform init -backend=false` and `terraform validate` used local state. The bucket, key, and lock table were not created.

The deployment workflow still assumes a role with GitHub OIDC and does not use long-lived access keys. It was not executed. The trust policy should stay limited to the intended repository and branch.

## Remaining vulnerabilities

- The local email sender records messages. It is not delivery to a mailbox, and Amazon SES is not enabled.
- A stolen access token remains valid until logout, password change, role change, deactivation, or expiry. Logout from the real session invalidates that stolen copy as well, because both copies share the stamp.
- Security-stamp checks add one primary-key read to authenticated requests.
- The development database does not yet have `DataProtectionKeys`. Start the new API against it only after the additive migration.
- Remote Terraform state, the GitHub OIDC role, the ACM certificate, and the JWT secret value do not exist yet.
- Rate limits are per source address. A large shared NAT can make unrelated users share a quota.
