# Testing guide

## Backend

```bash
export POSTGRES_HOST=localhost POSTGRES_PORT=5433 POSTGRES_USER=smartstaff
export POSTGRES_PASSWORD="$(docker exec smartstaff-postgres printenv POSTGRES_PASSWORD)"
dotnet build SmartStaff.sln -c Release
dotnet test SmartStaff.sln -c Release
```

The test host migrates `smartstaff_test`. It does not drop `smartstaff_db`. Test classes that share the host are marked non-parallel.

## Frontend

```bash
cd frontend
npm test
npx tsc -b --pretty false
npm run lint
npm run build
```

`npm test` runs Jest once. `src/pages/accessibility.test.tsx` renders the signed-out login and registration pages and the signed-in administrator views for facilities, requisitions, the clinician profile, the application pipeline, scheduling, attendance, notifications, and analytics. It runs axe-core. A passing run means those rendered states had no axe violations in jsdom. It does not measure a real browser viewport, a screen reader, or every dialog and error state.

## Isolated API

`scripts/seed-demo.mjs` drives the real API. It refuses port 5080 so it cannot sign in to the development API. Point `DEMO_BASE_URL` at `http://127.0.0.1:5081` after the production-style Compose stack is up. Passwords stay in the environment.

## What is not covered

VoiceOver or another screen reader, a full keyboard pass of every Material UI select and date field, and a contrast check outside the axe rules used in Jest. Do not treat the automated result as a WCAG conformance statement.
