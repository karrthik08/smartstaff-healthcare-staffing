# Product demonstration script

Two conversations. Both use the local site at http://127.0.0.1:8080 and fictional data from `scripts/seed-demo.mjs`. Speak in your own words. The beats below are there so the story stays short and accurate.

Do not describe SmartStaff as an employer’s product, and do not claim measured savings.

## Demo A — 3 minutes

For a recruiter or hiring manager.

**Open.** “I wanted to understand the staffing workflow, so I built a small working system. It is my own proof of concept. Nobody asked me to build it, and it is not a company system.”

**Problem.** “A facility needs a nurse, a recruiter has to move the candidate, and someone still has to lock the shift and approve the hours. Those steps are easy to lose when they live in different inboxes.”

**Show, slowly.** Sign in as the administrator and open the facility and the published nursing requisition. Sign in as the clinician and show the application. Sign in as the facility manager and show the confirmed shift and the approved timesheet. Open analytics and point at the placement and the scheduled shift.

**Value, without numbers.** “The useful part is that each handoff has a status in the same system. I have not measured time saved. What I can show is that the placement, the shift, and the approved hours are the same records the report is reading.”

**Close.** “I can walk through the engineering next, or we can stay on the workflow. I am still very interested in the role.”

## Demo B — 10 minutes

For an engineering manager or technical interviewer.

### 1. Architecture — about a minute

“The browser is React and TypeScript. It only talks to an ASP.NET Core API. nginx serves the built client and proxies `/api`, so the session cookie stays on one origin. PostgreSQL is the system of record. Entity Framework Core maps the workflow. Docker Compose runs this copy. There is an AWS sketch in the repo, and I have not provisioned it.”

### 2. Authentication and authorization

Sign in. “The access token is a JWT. It carries the account’s security stamp, and every request checks that stamp in PostgreSQL. When I sign out, the stamp changes, so the old token fails on the next call instead of waiting for the 15-minute expiry. Refresh uses an HTTP-only cookie plus a CSRF header.”

“Roles are Administrator, Facility Manager, Recruiter, and Clinician. Public registration can only create a clinician. The API also checks facility membership. A clinician who tries to create a facility gets 403. Hiding a button is not the control.”

### 3. Facility and requisition

As the administrator, open Facilities and Requisitions. “Creating a facility, a location, and a membership is three API calls. The requisition starts as Draft, moves to Open, then to Published. That transition is enforced on the server. The recruiter is assigned on the requisition, not inferred from the client.”

### 4. Application, pipeline, and placement

As the clinician, show the profile and the application. As the recruiter, show the pipeline through Submitted to Facility. As the facility manager, show Interview and Offer. “The placement is a separate record created from an offered application. Status changes are validated. You cannot skip the path by posting an arbitrary status.”

### 5. Scheduling, attendance, and timesheets

Show the shift for 2 June 2027, 07:00 to 15:00. “The client sends facility local time. The API converts America/Chicago to UTC and rejects a daylight-saving hour that does not exist or that occurs twice. The stored punch for 07:00 in June is 12:00 UTC.”

“Clock-in takes a PostgreSQL transaction advisory lock for that clinician, so two overlapping punches cannot both commit. The timesheet is a draft, then submitted, then approved. The clinician’s approve call returns 403. The manager’s approve call writes the approval history.”

### 6. Notifications and analytics

Open Notifications. “The business transaction writes an outbox row. A background worker polls it, creates the in-app notification, and stores an email record. It does not send mail to a provider. If delivery keeps failing, it stops after eight attempts.”

Open Analytics. “These counts are queries over the same tables. There is no separate warehouse. If the placement and the shift exist, the overview can show them. The caller only receives facilities they are allowed to see.”

### 7. Close on engineering judgment

“I kept one database so the workflow and the outbox commit together. I would not call that a production design. Before any real deployment I would want an external mail path, a hosted identity review, backups that someone has actually restored, and a security assessment. I have not done those, and I do not want to imply that I have.”
