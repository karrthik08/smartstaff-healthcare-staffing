# CI/CD

Workflows are prepared and have not been executed against GitHub. Do not add AWS access keys.

## Pull request workflow

`.github/workflows/ci.yml` runs on pull requests:

1. Restore and build the .NET solution in Release.
2. Run NUnit against a PostgreSQL 18 service container. The database password in that workflow is a CI-only value.
3. Install frontend dependencies.
4. Run TypeScript, Jest, oxlint, and the production frontend build.
5. Build the API and frontend Docker images. It does not push them.

## Deployment workflow

`.github/workflows/deploy.yml` runs only from `workflow_dispatch`. It requests an OIDC token and assumes `vars.AWS_ROLE_ARN`. It does not read `AWS_ACCESS_KEY_ID` or `AWS_SECRET_ACCESS_KEY`.

The job uses the GitHub environment named `production`. Create that environment, and these repository variables, only when deployment is approved:

- `AWS_ROLE_ARN`
- `AWS_REGION`
- `ECR_REPOSITORY`
- `ECS_CLUSTER`
- `ECS_SERVICE`
- `FRONTEND_BUCKET`
- `CLOUDFRONT_DISTRIBUTION_ID`
- `API_HEALTH_URL`
- `VITE_API_URL` when the browser must call a different origin

The job pushes the API image, waits for ECS to become stable, syncs the frontend, invalidates CloudFront, and exits non-zero when the health URL is not HTTP 200. A failed health check is the failure report for the deployment.

## Terraform

```sh
cd infra/terraform
terraform init -backend=false
terraform validate
```

`terraform plan` and `terraform apply` need AWS credentials and a real certificate ARN. Do not run apply from this preparation.
