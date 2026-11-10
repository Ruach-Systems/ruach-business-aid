# Business Overview

`/business-overview` exposes the existing RUACH Admin business inspection view to the selected business's owner. The desktop menu is **Business Overview**; mobile uses **Business**, with a building icon, between Home and Sales. Home remains the daily operational view.

The page reuses `AdminBusinessDetails` and `AdminPerformance` with an explicit workspace mode. Their default remains platform-admin mode. Both views share the profile, five financial figures, trend chart and exact-value table, period controls, and Summary/Sales/Expenses/Items/Receipts/Adjustments/Members sections. The existing read-only records and pagination are retained.

Workspace reads use authenticated `/api/businesses/{business}/overview/...` endpoints. These select workspace access internally; no query parameter can change authorization mode. Each read requires normal business management authorization plus an exact owner match. Platform administration alone grants no workspace overview access. Existing `/api/admin/...` endpoints still require platform-admin authorization.

Figures use synchronized server records and existing report calculations. Pending device changes are explicitly excluded. Current inventory is independent of selected dates. Financial and record responses are not persisted offline; disconnected users are prompted to connect. Local demo mode displays an explanation rather than requesting protected endpoints.

No schema, storage namespace, outbox, model version, or deployment configuration changes are required. The parent visual authority remains `../../ruach-brand-guide`; this page uses the existing product emblem and shared Admin detail styling without the platform-admin navigation shell.

## Local SQL-backed test sign-in

The API has an opt-in `/api/auth/local-test` form. It is registered only when the
host is Development, `LocalTestAuth:Enabled=true`, and the SQL connection uses
Windows authentication with `.\SQLDEVELOPER2025` / `MashalDevelopment`. Requests
must originate on loopback and use `localhost`. The POST requires a same-origin
request and an ASP.NET antiforgery token; GET does not sign in.

The form signs in only the synthetic `local-test-owner` subject with email
`local-owner@example.invalid`, unverified and non-admin. Provision this fixture
in the local database first. It never impersonates an arbitrary account.
Sessions last up to eight hours and use the existing cookie authorization.
This is separate from the client-only **Continue locally** demo.

For this checkout, the ignored `artifacts/run-api.ps1` starts the configured API
and `artifacts/seed-local-owner.sql` creates the fixture idempotently. Run SQL
with `sqlcmd -I` (quoted identifiers enabled). Neither file contains OAuth secrets.
Open http://localhost:5080/api/auth/local-test, sign in, open **Local Test Business**,
and select **Business Overview**. The new test business starts empty.
