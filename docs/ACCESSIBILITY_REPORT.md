# Accessibility report

## Automated check

`frontend/src/pages/accessibility.test.tsx` renders these views in Jest with jsdom and runs axe-core:

- Sign in
- Clinician registration
- Administrator dashboard
- Facilities
- Requisitions
- Professional profile
- Application pipeline
- Scheduling
- Attendance and timesheets
- Notifications
- Organization analytics

The pages are wrapped in `main`, which matches the application shell. The test mocks the API, so it checks structure, names, and the rules axe can run without a browser layout engine. It does not prove WCAG conformance.

The shell also has a skip link, a navigation landmark named Primary, `aria-current="page"` on the current item, and a visible focus outline on buttons and inputs. Sign-in errors use an alert that the email and password fields reference. The mobile navigation button is named Open navigation. Loading the route uses a progress indicator named Loading page.

## Fixes in this phase

- Primary navigation is a `nav` landmark.
- Keyboard focus on buttons and inputs uses a teal outline.
- Clock-in and clock-out helper text states that the value is facility local time.
- Timesheet start and end helper text states that those values are UTC.

## Still required

A person still needs to test with a screen reader, including VoiceOver, on login, dialogs, the notification menu, data tables, and error messages. Keyboard order through Material UI selects, date fields, and dialogs was not retested in a browser for this phase. Responsive behavior was not measured with a viewport screenshot pass in this phase. Color contrast outside the axe run in Jest was not measured with a separate analyzer.
