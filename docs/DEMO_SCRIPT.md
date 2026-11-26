# Demonstration script

Plan on 10 to 15 minutes. Use the isolated stack at http://127.0.0.1:8080. Do not sign in to the development site on port 5173 for this script, and do not change the existing development accounts.

The four roles are the bootstrap administrator plus three fictional accounts created by `scripts/seed-demo.mjs`. Each run uses new `@smartstaff.invalid` addresses that include a timestamp, so it does not reset or reuse an earlier demo password. The script prints those addresses. It does not print passwords.

Passwords are not in git. `DEMO_PASSWORD` must be at least 12 characters and include upper and lower case, a number, and a symbol. Keep it only in the shell. The administrator password is the existing bootstrap password for the isolated stack.

```bash
export DEMO_BASE_URL=http://127.0.0.1:5081
export DEMO_ADMIN_EMAIL="the bootstrap address"
export DEMO_ADMIN_PASSWORD="the bootstrap password"
export DEMO_PASSWORD="the shared demo password"
node scripts/seed-demo.mjs
```

The script refuses port 5080. It creates a new fictional facility named with a timestamp, so running it again adds a demo slice and does not delete prior rows. The names are fictional. Do not type a real clinician's information.

## Spoken path

1. Sign in as the administrator.
2. Open Facilities and show the new Demo General facility, its Demo Unit location, and the facility manager membership.
3. Show that the facility manager account is assigned on the administrator user list.
4. Open the Registered Nurse requisition and show that it is published.
5. Show the recruiter assignment on that requisition.
6. Sign out and sign in as the clinician. Open My profile and show the fictional ICU nurse profile.
7. Open the application created for that requisition.
8. Sign in as the recruiter and walk the pipeline through Submitted to Facility.
9. Sign in as the facility manager and show Interview and Offer, then the placement created by the recruiter.
10. On Scheduling, show the published shift for 2 June 2027, 07:00 to 15:00 facility time.
11. Sign in as the clinician and show the confirmed assignment.
12. On Attendance, show the clock-in at 07:00 and clock-out at 15:00 facility time. Explain that the database stores 12:00 and 20:00 UTC for America/Chicago in June.
13. Show the submitted timesheet. The hour fields for that entry were sent as UTC.
14. Sign in as the facility manager and show the approved timesheet.
15. Open Notifications and the bell. Items can take about 15 seconds to appear.
16. Open Analytics, filter to the demo facility, and show at least one placement and one scheduled shift.

Sign out at the end. After logout, the access token from that session no longer calls the API.

If a step fails, read the script's HTTP error and stop. Do not repair the demo by editing the development database.
