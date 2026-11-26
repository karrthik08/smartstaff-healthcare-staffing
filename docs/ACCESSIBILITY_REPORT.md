# Accessibility report

## What was reviewed

Login, registration, dashboards, facility management, recruitment, clinician profile, scheduling, attendance, timesheets, notifications, and analytics already use Material UI text fields with visible labels. The production login page exposes Email, Password, Sign in, Forgot password, and Create a clinician account as accessible names.

No automated axe scan was run across every route. The findings below are from the component review and the Jest auth flow, which renders the real login and registration pages.

## Fixes

- A skip link points at the main content.
- Navigation collapses into a dialog drawer below the medium breakpoint. The menu button is named Open navigation.
- The current page is marked with `aria-current="page"`.
- Sign-in failures use an alert, and the email and password fields point at that alert.
- The main region keeps a minimum width of zero so wide tables can shrink instead of forcing the page sideways.
- The loading boundary has the name Loading page.

## Not claimed

Color contrast was not measured with a contrast analyzer. Screen-reader behavior was not tested with VoiceOver. Keyboard use of the Material UI date and select controls was not retested in this phase. Those remain open checks before calling the interface fully accessible.
