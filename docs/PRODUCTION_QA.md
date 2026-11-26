# Production QA

This is a local production-style check. It is not an AWS deployment and it is not a statement that the service is ready for patients or customers.

## Automated tests

| Check | Result |
|---|---|
| `dotnet test SmartStaff.sln -c Release` | 30 passed, 0 failed |
| Jest | 35 passed, 9 suites |
| TypeScript and oxlint | Passed |
| Production frontend build | Passed |
| `terraform validate` | Passed after `terraform init -backend=false` |
| Terraform plan or apply | Not run |
| GitHub deployment workflow | Reviewed, not executed |
| Docker production images | Built for the isolated Compose project |

NUnit includes the logout regression: after logout, the previously issued access token is unauthorized.

## Isolated end-to-end workflow

The workflow ran against `http://127.0.0.1:5081` and PostgreSQL on `127.0.0.1:5434`. It did not call the development API on port 5080.

An administrator logged in, logged out, and the old access token received 401. New administrator, facility manager, recruiter, and clinician sessions were created. The clinician was forbidden from creating a facility. The administrator created a facility and location. The facility manager published a requisition. The administrator assigned the recruiter. The clinician saved a profile and applied. The recruiter moved the application through review and screening. The facility manager interviewed, offered, and placed the clinician, then published a shift. The clinician confirmed it, clocked in, clocked out, submitted a timesheet, and was forbidden from approving it. The facility manager approved it. The timesheet status was Approved.

Notifications were written to the outbox and delivered on a later poll, including shift published, shift assigned, and timesheet approved. The email path recorded those messages locally. It did not send them through Amazon SES. Restarting the API left the notification count at 58 before and after, and health returned 200.

The approved timesheet and the new facility were read back from PostgreSQL.

## Notification recovery

Delivery retries use exponential backoff and stop after 8 attempts. The worker logs the exception type. A restart does not delete notifications that were already stored. Idempotency keys keep a replay from creating a second in-app row for the same event.

## Readiness

The application can be built, tested, and run as an isolated production-style stack. It is not ready to deploy. AWS resources, the encrypted remote state backend, GitHub OIDC, an ACM certificate, a real JWT secret, and a production CORS origin are still absent. The development database has not received the data-protection migration. Email is still a local recording sink.
