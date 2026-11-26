# Known limitations

- SmartStaff has not been deployed, penetration tested, or reviewed as a production clinical system. Do not describe it as HIPAA compliant or production ready.
- Logout rejects older access tokens by rotating the security stamp. A stolen access token still works until that rotation or until the token expires. The default lifetime is 15 minutes.
- Clock-in and clock-out accept facility local time and store UTC. Timesheet entry hours are still submitted as UTC.
- Daylight-saving gaps and repeated hours are rejected. The API does not guess which offset an ambiguous hour meant.
- Existing attendance rows are left unchanged by the local-time change.
- Email is written to `EmailDispatches` by `RecordingEmailSender`. Amazon SES is not registered or provisioned.
- Password reset in local development can use a log sink. The production-style Compose file turns that sink off. There is still no external mailer.
- Automated axe checks cover the main rendered pages in Jest. They do not prove WCAG conformance. Screen-reader testing, including VoiceOver, has not been completed. Keyboard behavior of every Material UI select, date, and dialog still needs a manual pass.
- Analytics and notifications are only as current as the database and the notification worker. The worker polls about every 15 seconds.
- Terraform targets PostgreSQL 16 by default. Local containers run PostgreSQL 18. Those versions have not been compared in a live RDS instance.
- The development database on port 5433 contains earlier verification accounts. Demo seeding must use the isolated API on port 5081 and must not change those accounts.
