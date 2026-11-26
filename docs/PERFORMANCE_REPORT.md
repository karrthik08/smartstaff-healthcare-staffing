# Performance report

## Method

k6 v2.4.0 ran against the isolated API at `http://127.0.0.1:5081`. Ten virtual users ran for 20 seconds. Each iteration, after one login in setup, requested facilities, requisition search, shifts, timesheets, notifications, and analytics overview. The isolated database was used. The development database on port 5433 was not a target. The isolated API rate limit was raised to 100,000 a minute for this run so the limiter would not be the thing being measured. The production default remains 2,000 a minute.

## Results

| Measurement | Result |
|---|---|
| Requests | 15,703 |
| Throughput | 780 requests per second |
| Failed requests | 0 |
| Checks | 15,702 passed, 0 failed |
| Average latency | 12.64 ms |
| Median latency | 9.27 ms |
| P95 latency | 29.77 ms |
| Maximum latency | 496.7 ms |
| API CPU during the run | peaked at about 307% of one core |
| API memory | peaked at about 228 MiB |
| PostgreSQL CPU | peaked at about 202% of one core |
| PostgreSQL memory | about 74 MiB |
| Database connections to `smartstaff_db` | 11 throughout the samples |

The pool default is 20 connections. The sample stayed at 11 connections for 10 concurrent users, so the test did not exhaust the pool.

## Query changes

Analytics overview was included in the timed requests. On this data set its contribution did not push P95 above 30 ms. No query or index change was made. A larger production data set should be measured again before adding indexes.

## Frontend

The production build remains split. The initial JavaScript is the application chunk at 76.44 kB, React at 258.37 kB, and MUI at 284.33 kB. Recharts stays in the lazy analytics chunk at 439.45 kB. Hashed files under `/assets/` are cached for one year. `index.html` is not cached. No dependency was added.
