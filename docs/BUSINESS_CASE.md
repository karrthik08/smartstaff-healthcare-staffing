# Business case

SmartStaff is an independent proof of concept. The challenges below are general healthcare staffing problems. This document does not say that any employer currently has them, and it does not estimate savings, productivity, or revenue.

## Industry context

Healthcare facilities fill shifts with their own staff and with clinicians placed through a staffing team. A typical path runs from an open role, to recruiting, to a facility decision, to a scheduled shift, to worked hours and approval. Several people own different parts of that path.

## Common healthcare staffing challenges

These are industry patterns, not findings about a specific company:

- **Fragmented workflows.** Requisitions, candidate status, and the schedule often live in different files or inboxes.
- **Manual recruiting coordination.** Status changes depend on someone remembering to tell the next person.
- **Clinician availability.** A person can look free in one list and already be committed in another.
- **Shift conflicts.** Two assignments can overlap when nobody checks both calendars.
- **Timesheet delay.** Hours sit in email waiting for a manager who does not have the shift in front of them.
- **Limited visibility.** A leader cannot see how many jobs are published, how many shifts are filled, or how many timesheets are waiting without asking each team.
- **Thin operational reporting.** Counts are assembled by hand after the work is already done.

## Proposed SmartStaff solution

SmartStaff keeps the path in one application with four roles. The facility, the requisition, the application, the placement, the shift, the attendance record, and the timesheet refer to each other. Each role sees the actions it is allowed to take.

What the application demonstrates today:

- A facility and a published requisition.
- An application moving through a defined set of statuses to a placement.
- A shift that is published, assigned, and confirmed.
- Attendance stored from the facility’s local clock time.
- A timesheet that a clinician submits and a facility manager approves.
- Notifications for those events, and analytics read from the same records.

What it does not demonstrate:

- A reduction in cost or time.
- Integration with a credentialing body, a payroll system, or an HR system.
- A production deployment.

## End-to-end workflow

1. An administrator creates the facility and assigns a manager and a recruiter.
2. The requisition moves from draft to open to published.
3. A clinician applies with a profile.
4. The recruiter reviews the application and submits it to the facility.
5. The facility manager records the interview and the offer.
6. The recruiter creates the placement.
7. The manager schedules a shift. The clinician confirms it.
8. The clinician records attendance and submits a timesheet.
9. The manager approves the timesheet.
10. Notifications and analytics reflect the rows that were written.

## Technical implementation

The workflow is a set of REST operations on ASP.NET Core, with PostgreSQL as the system of record and EF Core as the persistence layer. Authorization is enforced in the API, not only in the React screens. Attendance and shift changes use database transactions. Clock-in uses a transaction advisory lock so two overlapping punches do not both succeed. Notification records are written from an outbox so a saved business change is not lost if the email-style delivery step happens later. In this build, that delivery step stores the message. It does not send external mail.

## Potential business value

If a team adopted a system of this shape, the plausible benefits would be operational rather than automatic:

- One status for a requisition, an application, a shift, and a timesheet.
- A clearer handoff between recruiter, facility, and clinician.
- Fewer overlapping shifts, because the API rejects an assignment that conflicts.
- A shorter path from worked hours to an approval decision, because the timesheet is tied to the placement and the shift.
- A report that counts what is already in the database.

Those are hypotheses. This project has not measured them.

## Limitations

The interface is a proof of concept. Some forms ask for identifiers instead of offering a finished scheduling calendar. Email does not leave the application. There is no mobile-native client, no payroll export to a vendor, and no claim of regulatory compliance. The hosted cloud design has not been provisioned.

## Possible enterprise integrations

A later evaluation could consider, separately from this codebase:

- Single sign-on with an employer identity provider.
- An external email or messaging system.
- Credential document storage and primary-source verification.
- Payroll or accounts-payable export.
- A calendar feed for clinicians.
- A hosted environment with backups, monitoring, and an access review.

Each of those would be new work. None of them is required to run the local demonstration.
