# API

The API is JSON over HTTP. Enums are strings. List endpoints default to page 1 and page size 10, with a maximum page size of 50 unless a controller sets its own default. Authenticated calls send `Authorization: Bearer`. Logout and refresh also require the refresh cookie and a matching `X-CSRF-TOKEN` header.

A healthy process with a reachable database returns 200 from `GET /api/health`.

## Accounts

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/auth/register` | Create a clinician. Other requested roles are rejected. |
| POST | `/api/auth/login` | Return an access token and set the refresh and CSRF cookies. |
| POST | `/api/auth/refresh` | Rotate the refresh cookie. |
| POST | `/api/auth/logout` | Revoke the refresh family and rotate the security stamp. |
| GET | `/api/auth/me` | Current user. |
| POST | `/api/auth/forgot-password` | Start a reset. The response does not reveal whether the email exists. |
| POST | `/api/auth/reset-password` | Complete a reset. |
| GET | `/api/admin/users` | Administrator user search. |
| PUT | `/api/admin/users/{id}/roles` | Replace assigned roles. |
| PUT | `/api/admin/users/{id}/status` | Activate or deactivate. |
| GET | `/api/admin/audit-logs` | Recent audit rows. |
| GET | `/api/dashboards/administrator` | Administrator workspace. Recruiter, clinician, and facility have their own paths. |

## Staffing and recruitment

| Method | Path | Purpose |
|---|---|---|
| POST, GET, PUT | `/api/facilities` | Create, list, and update facilities. |
| POST | `/api/facilities/{id}/locations` | Add a location. |
| POST | `/api/facilities/{id}/members` | Add a manager or recruiter membership. |
| POST, GET | `/api/requisitions` | Create and list requisitions. |
| PATCH | `/api/requisitions/{id}/status` | Change status. Draft moves to Open, then Open moves to Published. |
| PATCH | `/api/requisitions/{id}/assign-recruiter` | Assign a recruiter. |
| PUT | `/api/clinicians/me` | Save the signed-in clinician profile. |
| POST | `/api/applications` | Apply to a requisition. |
| PATCH | `/api/applications/{id}/status` | Move the pipeline. |
| POST | `/api/placements` | Place an offered application. |

## Scheduling and time

| Method | Path | Purpose |
|---|---|---|
| POST | `/api/shifts` | Create a draft from facility-local start and end. |
| PATCH | `/api/shifts/{id}/publish` | Publish a draft. |
| POST | `/api/shifts/{id}/assignments` | Assign a clinician profile. |
| PATCH | `/api/shifts/assignments/{id}/confirm` | Clinician confirms. |
| POST | `/api/attendance/{assignmentId}/clock-in` | Clock in. `local` is facility local time. `at` remains an offset for existing clients. |
| POST | `/api/attendance/{assignmentId}/clock-out` | Clock out with the same two fields. |
| POST | `/api/timesheets` | Open a timesheet for a facility and period. |
| POST | `/api/timesheets/{id}/entries` | Add hours. `startTime` and `endTime` include an offset. |
| POST | `/api/timesheets/{id}/submit` | Submit. |
| POST | `/api/timesheets/{id}/approve` | Manager approval. |
| POST | `/api/timesheets/{id}/reject` | Manager rejection with a reason. |

## Notifications and analytics

| Method | Path | Purpose |
|---|---|---|
| GET | `/api/notifications` | Notification page for the caller. |
| PATCH | `/api/notifications/{id}/read` | Mark one read. |
| PATCH | `/api/notifications/read-all` | Mark all read. |
| GET, PUT | `/api/notifications/preferences` | Read and save preferences. |
| GET | `/api/analytics/overview` | Summary counts. |
| GET | `/api/analytics/{recruitment,scheduling,attendance,timesheets,credentials}` | Report sections. |

Filters on analytics accept `from`, `to`, and `facilityId`. The caller only sees facilities they are allowed to see.
