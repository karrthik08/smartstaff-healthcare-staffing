# Sharing options

Everything below stays at zero cost. Do not buy a domain, turn on a paid GitHub feature, or create cloud resources for the demonstration.

## Option A — Private GitHub repository and a short recording

| | |
|---|---|
| Cost | $0. A private repository on the free GitHub plan is enough. Do not enable paid Actions minutes, Codespaces, or GitHub Advanced Security. |
| Setup | Create `smartstaff-healthcare-staffing` as private, push `main`, and record a 3-minute screen capture of the local site. |
| Reliability | High. The recording does not depend on a live meeting or on the reviewer’s network. The repository remains available after the call. |
| Security | Private means the code is limited to people you invite. Do not include the env file, database dumps, or passwords. The CI workflow uses a disposable database password and is not a production secret. |
| What they can access | Source, documentation, and the recording. They cannot reach your laptop or your database unless you also choose Option B. |

## Option B — Live screen share from local Docker

| | |
|---|---|
| Cost | $0. The stack is already defined in `docker-compose.prod.yml`. |
| Setup | Start the existing containers, run the health checks, seed fictional users, and share your screen. |
| Reliability | Good while your machine and Docker are up. A sleep, a VPN change, or a full disk can interrupt it. There is no public URL to fall back to. |
| Security | The application stays on `127.0.0.1`. Viewers see only what you share. Do not share a terminal that prints `POSTGRES_PASSWORD` or `DEMO_PASSWORD`. |
| What they can access | The live screens you navigate. They do not receive the code unless you also use Option A. |

## Option C — Free public demo hosting

| | |
|---|---|
| Cost | A free tier can be $0 until a limit is exceeded. This application needs a .NET process and PostgreSQL, which most static free hosts do not provide together. A free database is often paused or deleted. |
| Setup | High. You would still need a public URL, cookie settings for HTTPS, and a place for secrets. That work is not done, and this document does not depend on it. |
| Reliability | Low for an interview. Free databases sleep, and a cold start during a call is a poor failure mode. |
| Security | A public URL exposes the sign-in form to the internet. The demonstration uses fictional data, but the API would still need rate limits, HTTPS, and a secret store that this project has not deployed. |
| What they can access | Anyone with the URL, unless you add access control that is not configured. |

## Recommendation

Use Option A and Option B together, and skip Option C.

Create the private repository when you are ready, and run the live walkthrough from Docker. If the meeting is short, send the private repository and a 3-minute recording instead of asking the reviewer to reach a server. That combination costs nothing, keeps the database on your machine, and still lets a technical reviewer read the code.

Do not run the publish commands until you want the private repository created. Do not make the repository public.

```bash
gh repo create smartstaff-healthcare-staffing --private --source=. --remote=origin --description "Independent healthcare staffing proof of concept"
git push -u origin main
```

There is no Git remote today. GitHub CLI is signed in and can create a private repository when you choose to run those commands. Do not add `--public`.
