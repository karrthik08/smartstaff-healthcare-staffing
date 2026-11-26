# Database schema

The schema is created by EF Core migrations. The latest migration, `20261126214016_DataProtectionKeys`, adds only `DataProtectionKeys`.

Development data lives in `smartstaff_db` on `localhost:5433`. Tests use `smartstaff_test` on that same server. The production-style Compose stack uses another server on `127.0.0.1:5434`.

## Identity and security

`Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `UserTokens`, `RoleClaims`, `RefreshTokens`, `PasswordResetTokens`, `AuditLogs`, and `DataProtectionKeys`.

`Users` stores the security stamp used to reject old access tokens. Refresh token secrets and password-reset tokens are stored as hashes.

## Staffing

`HealthcareFacilities` includes `TimeZoneId`. `FacilityLocations` and `FacilityMemberships` belong to a facility. Membership roles are `Manager` and `Recruiter`. `JobRequisitions` belong to a facility and a location and may name an assigned recruiter.

## Recruitment

`ClinicianProfiles`, `ClinicianCredentials`, `JobApplications`, `ApplicationStatusHistory`, and `Placements`.

Application statuses used by the product are Applied, UnderReview, Screening, SubmittedToFacility, Interview, Offer, Placed, Rejected, and Withdrawn.

## Scheduling and time

`Shifts`, `ShiftAssignments`, `ClinicianAvailability`, and `ShiftChangeRequests`.

`AttendanceRecords` store `ClockInTime` and `ClockOutTime` as offsets. The facility timezone is used to display them and to convert a local clock value. Historical rows are not migrated when display rules change.

`Timesheets`, `TimesheetEntries`, `TimesheetApprovalHistory`, `TimesheetAdjustments`, and `OvertimePolicies`.

## Notifications

`NotificationTemplates`, `NotificationOutbox`, `Notifications`, `NotificationDeliveries`, `NotificationPreferences`, and `EmailDispatches`.

## Migrations

`__EFMigrationsHistory` records applied migrations. Apply pending migrations with `scripts/apply-migrations.sh` after a backup. Do not run `Down`. Several historical down methods drop tables.
