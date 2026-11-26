# Executive overview

SmartStaff is an independent proof of concept for healthcare staffing operations. It is not an official product, and no employer requested or sponsored it. It is a working local application, not a production deployment.

## Business problem

Filling a shift takes more than finding a name. A facility opens a role, a recruiter moves a candidate, the facility decides, someone schedules the shift, and the hours still have to be approved. When those steps are split across inboxes, status is easy to lose.

This describes a general staffing problem. It is not a finding about any company’s current process.

## Solution

SmartStaff gives an administrator, a facility manager, a recruiter, and a clinician one system. They share the facility, the requisition, the application, the placement, the shift, the attendance record, and the timesheet. Each person sees only the actions their role allows.

## Main workflows

Publish a requisition. Accept an application. Move it through review to an offer and a placement. Schedule and confirm a shift. Record attendance in the facility’s local time. Submit and approve a timesheet. Read the notifications and the analytics that come from those records.

## Technical architecture

The client is React and TypeScript. The API is ASP.NET Core. PostgreSQL, through Entity Framework Core, is the system of record. Sign-in uses JWT access tokens and a server-checked security stamp, plus role and facility checks. Notifications are written from a transactional outbox by a background worker. Docker Compose runs the demonstration on one machine. A possible cloud layout is documented and has not been created.

## What is demonstrated

A local run can create a facility, publish a job, process an application, place a clinician, confirm a shift, store attendance in UTC, approve a timesheet, show notifications, and return analytics for that facility. Automated tests cover the API and the main screens. An accessibility scan of the rendered pages found no axe-core violations. That scan is not a WCAG certification.

## Potential value

The practical value, if a team later adopted this shape of system, would be one status for each handoff and a report that reads the same records. No time saved, cost saved, or revenue change has been measured.

## Next steps for an evaluation

A serious evaluation would be separate from this repository. It would need an identity and security review, a decision about external email and payroll, a backup restore test, and a hosting choice. None of that is required to see the local demonstration. The simplest way to see it is a private code review plus a screen share from the Docker environment already running on the developer machine.
