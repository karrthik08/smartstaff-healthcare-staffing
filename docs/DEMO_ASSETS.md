# Demonstration assets

These screens were captured from http://127.0.0.1:8080 on 26 November 2026. The accounts are fictional `@smartstaff.invalid` users created by `scripts/seed-demo.mjs`. The login screen was captured before any credentials were typed. No password is visible.

| Screen | File |
|---|---|
| Login | [screenshots/login.png](screenshots/login.png) |
| Administrator dashboard | [screenshots/administrator-dashboard.png](screenshots/administrator-dashboard.png) |
| Facility management | [screenshots/facilities.png](screenshots/facilities.png) |
| Requisition management | [screenshots/requisitions.png](screenshots/requisitions.png) |
| Recruitment pipeline | [screenshots/recruitment-pipeline.png](screenshots/recruitment-pipeline.png) |
| Clinician workspace | [screenshots/clinician-dashboard.png](screenshots/clinician-dashboard.png) |
| Scheduling | [screenshots/scheduling.png](screenshots/scheduling.png) |
| Attendance and timesheets | [screenshots/timesheets.png](screenshots/timesheets.png) |
| Notifications | [screenshots/notifications.png](screenshots/notifications.png) |
| Analytics | [screenshots/analytics.png](screenshots/analytics.png) |

The administrator header shows the isolated bootstrap display name already stored in that database. The other headers show Casey Demo, Riley Recruiter, and Morgan Manager.

## What a later capture should still include

If you reseed, capture the same ten routes after sign-in and sign-out between roles:

1. `/login` with empty fields.
2. `/dashboard/admin` as the administrator.
3. `/facilities`.
4. `/requisitions`.
5. `/applications` as the recruiter.
6. `/dashboard/clinician`.
7. `/schedule` as the clinician.
8. `/attendance` showing the completed punch and the approved timesheet.
9. `/notifications`.
10. `/analytics` as the facility manager.

Do not photograph a filled password field, a terminal, or an env file.

## How the captured screens read

- Facilities lists Demo General and Demo Unit in Austin.
- Requisitions lists Registered Nurse jobs. A requisition that already has a placement can read Filled.
- The pipeline shows Casey Demo as Placed.
- The schedule shows the 2 June 2027 ICU shift as confirmed in America/Chicago.
- Attendance shows that shift in facility local time and an approved timesheet of 7.5 regular hours.
- Notifications include the published shift, the assignment, and the approved timesheet.
- Facility analytics shows 1 placement, 1 scheduled shift, and 8 worked hours.

The timesheet card also prints an entry identifier. That is the current interface, not a hidden credential.
