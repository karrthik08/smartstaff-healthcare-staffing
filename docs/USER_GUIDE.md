# User guide

Sign in at `/login`. Public registration creates a clinician only. An administrator assigns Recruiter and Facility manager from the administrator dashboard.

## Administrator

The administrator dashboard lists users, roles, active status, and the audit log. Administrators can create a facility, add a location, add a facility manager, create a requisition, assign a recruiter, and publish the requisition. They can also open analytics.

## Facility manager

A facility manager sees facilities they belong to. They review applications that reach the facility, move an application to Interview and Offer, publish and assign shifts, and approve or reject submitted timesheets.

## Recruiter

A recruiter works requisitions assigned to them. They move a new application through Under Review, Screening, and Submitted to Facility. After the facility offer, they create the placement.

## Clinician

A clinician completes My profile, applies to a published job, confirms an assigned shift, and records attendance. Clock-in and clock-out use the facility's local time. The server stores UTC. A time that falls in a daylight-saving gap, or that occurs twice when clocks fall back, is rejected and must be entered again as an unambiguous hour.

Timesheet start and end are still UTC. After the hours are saved, the clinician submits the timesheet. A clinician cannot approve it.

## Notifications

The bell opens recent items and links to the related page. The Notifications page lists the same items, marks them read, and stores preferences. Email delivery is stored inside the application and is not sent to an outside mailbox in this build.

## Dates

Scheduling and the clock form follow the facility timezone. Analytics dates are calendar dates. The interface shows local browser time for some audit and dashboard timestamps. Attendance values displayed after clock-in are the facility-local equivalents of the stored UTC values.
