# MASHAL Business Aid

A .NET 10 Blazor WebAssembly PWA with offline operations and a separate ASP.NET Core API using Dapper and SQL Server.

- PWA: https://businessaid.mashalsystems.com — Firebase Hosting
- API: https://api.businessaid.mashalsystems.com — MyASP.NET
- Authentication: API-hosted Google OAuth and HttpOnly session cookies
- Device data: IndexedDB `mashal-sql-v2`, with a durable outbox
- Schema updates: standalone DbUp runner, never API startup

## Simple solution structure

Open `Mashal.BusinessAid.slnx` in Visual Studio, Rider, or VS Code.

```text
src/
  Mashal.BusinessAid.Client/       Feature-grouped Pages, Components, Layout, Services, wwwroot
  Mashal.BusinessAid.Api/          Configuration, Middleware, Endpoints, Data
  Mashal.BusinessAid.Shared/       Models, Contracts, Rules, Offline
tools/
  Mashal.BusinessAid.Migrations/   DbUp console runner
tests/
  Mashal.BusinessAid.Tests/        Unit, component, SQL, HTTP and browser tests
database/migrations/              Immutable ordered SQL scripts
docs/                            Code navigation and maintenance guide
```

The Client and API reference Shared. The Client never references SQL or the API assembly. Shared commands use either a local in-memory repository for provisional projections or a business-scoped Dapper repository inside the server transaction. Server authorization and transactions remain in the API. There is no mediator, generic application framework, or extra architecture layer.

See [the code structure guide](docs/code-structure.md) for where to make changes and the contracts to preserve. DbUp remains the migration runner, including in validation and deployment; no manual SQL procedure is required.

## Run locally: no npm or pnpm required

Install the .NET 10 SDK and SQL Server 2022 or later. Node.js is not needed to build, run, or test the application.

In PowerShell, start from the repository root:

```powershell
cd "D:\WORK\MY APPS\My Business\mashal-business-aid"
dotnet restore Mashal.BusinessAid.slnx
```

Create an empty database named `MashalDevelopment` in SSMS. If SQL command-line tools are installed:

```powershell
sqlcmd -S ".\SQLDEVELOPER2025" -E -C -Q "IF DB_ID('MashalDevelopment') IS NULL CREATE DATABASE MashalDevelopment"
```

Replace the instance name if yours is different.

In terminal 1, migrate and run the API:

```powershell
$env:ConnectionStrings__Mashal = 'Server=.\SQLDEVELOPER2025;Database=MashalDevelopment;Integrated Security=true;TrustServerCertificate=true'
dotnet run --project tools/Mashal.BusinessAid.Migrations
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:App__Origin = 'http://localhost:5173'
$env:Google__ClientId = '<your-google-client-id>'
$env:Google__ClientSecret = '<your-google-client-secret>'
$env:MashalAdmin__Emails__0 = '<your-verified-google-email>'
dotnet watch --project src/Mashal.BusinessAid.Api run
```

The API listens on http://localhost:5080. Check `/health/live` and `/health/ready`. Use credentials from a Google OAuth web application; invalid placeholder values will cause Google to reject sign-in.

In terminal 2:

```powershell
dotnet watch --project src/Mashal.BusinessAid.Client run
```

Open http://localhost:5173. Use **Continue locally** to test operations without Google or SQL synchronization. For UI/demo testing alone, only terminal 2 is needed. Local mode intentionally has no authoritative financial reports.

Use `localhost` consistently: `127.0.0.1` is a different browser origin. The development client retains port 5173 so existing SQL-era IndexedDB data remains available.

Register this Google OAuth development redirect URI:

```text
http://localhost:5080/api/auth/google/callback
```

Replace the two Google environment variables with actual credentials and restart the API to test real sign-in. The old port-5173 callback is no longer used. Production callback remains `https://api.businessaid.mashalsystems.com/api/auth/google/callback`.

The development API URL and optional reset control are in Client/wwwroot/appsettings.Development.json. Reset is disabled by default. Local/demo mode and reset are unavailable in Release builds. No SQL connection string, OAuth secret, or certificate belongs in Client configuration.

## Tests and published PWA verification

```powershell
dotnet build Mashal.BusinessAid.slnx
dotnet test tests/Mashal.BusinessAid.Tests --filter "Category!=Integration&Category!=Browser"
```

Use a dedicated database for integration tests, never development business data or production:

```powershell
sqlcmd -S ".\SQLDEVELOPER2025" -E -C -Q "IF DB_ID('MashalMigrationTests') IS NULL CREATE DATABASE MashalMigrationTests"
$env:ConnectionStrings__Mashal = 'Server=.\SQLDEVELOPER2025;Database=MashalMigrationTests;Integrated Security=true;TrustServerCertificate=true'
dotnet run --project tools/Mashal.BusinessAid.Migrations
dotnet test tests/Mashal.BusinessAid.Tests --filter "Category=Integration"
dotnet run --project tools/Mashal.BusinessAid.Migrations -- --verify-no-pending
```

The tests insert isolated synthetic users/businesses and migration test tables. The migration verification must report **Pending migrations: 0**.

Blazor's development worker intentionally does not cache the app. Test offline reload and updates using the published PWA. When changing publish directories, first run `dotnet clean src/Mashal.BusinessAid.Client -c Release` to avoid stale incremental service-worker output. Always run the release verification script before deployment. Browser tests default to `artifacts/pwa/wwwroot`; set `MASHAL_PWA_ROOT` to an absolute published wwwroot path to test another bundle.

```powershell
dotnet publish src/Mashal.BusinessAid.Client -c Release -o artifacts/pwa
dotnet build tests/Mashal.BusinessAid.Tests
powershell -NoProfile -ExecutionPolicy Bypass -File tests/Mashal.BusinessAid.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
$env:MASHAL_BROWSER_TESTS = 'true'
dotnet test tests/Mashal.BusinessAid.Tests --filter "Category=Browser"
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify-release.ps1
```

The PowerShell policy option applies only to that child process. No account-wide execution policy change is necessary. If Microsoft Edge is already installed, set `$env:MASHAL_BROWSER_CHANNEL = 'msedge'` to use it instead of downloading Chromium.

Browser tests start an isolated localhost preview of the published assets and use fixture API responses. They cover 320/390/768/1366px layouts, core forms, old IndexedDB state, frozen retries, offline operations/reloads, concurrent tabs, and a service-worker upgrade. Screenshots are written to `artifacts/browser`. These tests do not replace actual Google sign-in and installed-device acceptance.

To publish for another environment, pass its public API origin:

```powershell
dotnet publish src/Mashal.BusinessAid.Client -c Release -o artifacts/pwa -p:ApiOrigin=https://api.test.businessaid.mashalsystems.com
```

This embeds only the public URL in the client assembly. It is never a secret. CI uses this option before validating and uploading the artifact.

## Offline data and updates

The Client reuses IndexedDB `mashal-sql-v2` version 1 and its `accounts`/`meta` stores. Existing SQL-era state and prepared outbox payloads are compatible. A revision property supports atomic compare-and-swap saves across tabs; older records default to revision zero. JavaScript is limited to browser storage, locks, connectivity events, and service workers. C# owns calculations and synchronization.

A save commits its projection and operation to IndexedDB before success is shown. Synchronization runs in the background. A failed local write is shown as a save error. Server retries preserve the same operation ID and payload, and the client removes an operation only after pulling and durably storing the authoritative changes.

The first visit must be online to download the PWA. Subsequent published releases check at startup, focus, visibility changes, reconnect, and every 15 minutes. A fully downloaded update activates and reloads the app once. Shell caches and IndexedDB are independent. Do not clear browser site data when testing persistence.

A frontend replacement on the same origin preserves the installation. Moving from the old web.app domain to the custom domain is a separate origin and needs opening/installing that domain once. Firebase-era records are not imported or deleted.

## Branding

The approved exports remain in `public/brand`; master geometry and fonts remain in `scripts/brand-source`. The Client uses the existing content-hashed logo and launcher exports in `wwwroot/brand-assets`. Keep one geometry source and reuse those exports. The release check verifies filenames against SHA-256 and confirms every image is in the offline manifest. If a brand export changes, update its hashed copy and corresponding references together. No asset generator or Node package is required for normal builds.

## DbUp migration procedure

1. Add the next ordered script under `database/migrations`, such as `002_AddReportingIndex.sql`.
2. Never edit a successfully applied script. Use a new forward correction.
3. Test a fresh database and an upgrade, then rerun and verify zero pending scripts.
4. Use additive expand-and-contract SQL so migrations remain compatible with the currently deployed API.
5. DbUp executes each script transactionally and journals success in `dbo.SchemaVersions`. Failure exits nonzero and blocks deployment.
6. Keep scripts SQL Server/DbUp-compatible: no SQLCMD directives, USE statements, credentials, or manual transaction wrappers.

`001_InitialSchema.sql` is the consolidated baseline for a **new, empty database**, including recipe batch yields and multi-business accounts. The runner embeds it as `Mashal.Migrations.001_InitialSchema.sql`. Future changes start at `002` and remain forward-only.

This fresh-start baseline replaces the former three-script chain; it is not an upgrade for a database created by that chain. DbUp skips scripts already recorded in `dbo.SchemaVersions`, so an existing `001` entry will not apply this consolidated definition. Use a new database for the fresh start; do not clear an existing journal or rerun the baseline over existing business data.

Development, Test, and Production connection strings are separate environment secrets. Migrations are run explicitly; the web API never invokes DbUp.

Production preflight creates a COPY_ONLY backup with checksums and executes RESTORE VERIFYONLY. `SQL_BACKUP_PATH` must be a SQL Server-side directory. Confirm backup/verify permissions with MyASP.NET. If the hosting plan blocks this procedure, arrange a supported automated backup process before enabling Production deployment; do not bypass the check.

A failed migration leaves the old application deployed. Correct the unapplied script, or add a new forward migration if it already ran anywhere. Retain expanded schemas when rolling an application back. Database restores are deliberate administrative operations. Keep backups under an explicit retention policy and periodically prove recovery by restoring to a separate test database.

## GitHub Actions and environments

`validate.yml` restores and builds the .NET solution, runs shared/client/component tests, applies DbUp to an isolated SQL Server, runs integration tests, verifies the journal, publishes the PWA, checks its assets, runs .NET Playwright offline/update tests, and publishes the exact API/PWA/migration artifacts.

`deploy.yml` is manually dispatched for Development, Test, or Production. It first invokes the validation workflow for the selected commit and API origin, then runs distinct jobs:

1. Database connectivity and production backup verification
2. Pending DbUp migrations
3. MyASP.NET API deployment and readiness check
4. Firebase Hosting deployment of the validated PWA artifact
5. Public release smoke checks

The workflow serializes deployments per environment and never cancels an active deployment. Configure Production required reviewers and deployment branch protection in **GitHub Settings → Environments**. YAML alone cannot enable required reviewers.

### Required GitHub Environment settings

Create all three environments with separate databases, OAuth clients where appropriate, and deployment targets.

| Setting | Kind | Purpose |
|---|---|---|
| SQL_MIGRATION_CONNECTION_STRING | Secret | Migration and backup login |
| SQL_APP_CONNECTION_STRING | Secret | API login; DML access only, no schema/backup privileges |
| SQL_BACKUP_PATH | Secret | Server-side backup directory, required for Production |
| GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET | Secrets | Google OAuth application |
| DATA_PROTECTION_CERTIFICATE_BASE64 | Secret | Base64 PKCS#12 certificate protecting persisted cookie keys |
| DATA_PROTECTION_CERTIFICATE_PASSWORD | Secret | Password for that certificate |
| WEBDEPLOY_ENDPOINT | Secret | HTTPS endpoint from MyASP.NET publish settings |
| WEBDEPLOY_SITE | Secret | Exact IIS site name |
| WEBDEPLOY_USERNAME / WEBDEPLOY_PASSWORD | Secrets | MyASP.NET Web Deploy credentials |
| FIREBASE_SERVICE_ACCOUNT | Secret | Firebase Hosting service account JSON |
| API_ORIGIN | Variable | Environment API HTTPS origin |
| PWA_ORIGIN | Variable | Environment PWA HTTPS origin |
| MASHAL_ADMIN_EMAILS | Variable | Comma-separated, verified Google email addresses allowed to use Mashal Admin |
| FIREBASE_PROJECT_ID | Variable | Environment-specific Firebase hosting project |

Keep the selected dispatch API origin identical to the environment API_ORIGIN; preflight checks this. Recommended nonproduction naming is `dev.businessaid.mashalsystems.com` / `api.dev.businessaid.mashalsystems.com`, and equivalent `test` names. Use separate Firebase projects to avoid deployments replacing another environment.

The API artifact contains no secrets. At deployment, the script inserts secrets into the IIS-protected web.config on the runner, publishes it over HTTPS, and removes them from the runner artifact afterward. Never upload that generated configuration or publish settings. API `App_Data/keys` is preserved by Web Deploy so existing sessions survive deployment; keep the certificate stable, protect it, and back it up separately. All hosted environments run with ASPNETCORE_ENVIRONMENT=Production security behavior.

MyASP.NET must support the selected .NET 10 runtime, out-of-process hosting, Web Deploy, writable private App_Data, HTTPS, SQL connectivity from its API and from deployment runners, and the backup procedure. Configure SQL/network restrictions in the host where supported.

## Domains and first production cutover

1. Register mashalsystems.com and configure DNS.
2. Add businessaid.mashalsystems.com to Firebase Hosting and enter Firebase's verification/routing records.
3. Add api.businessaid.mashalsystems.com to MyASP.NET and point it to the assigned server.
4. Provision valid HTTPS certificates independently for each hostname.
5. Configure the exact Google callback, PWA origin, SQL database, and GitHub Environment settings.
6. Deploy Test, validate authenticated workflows on actual devices, then approve Production.
7. Direct users from the old web.app address to the new domain with a clear fresh-start notice. Do not silently upload old browser data.
8. Retire old Firebase Authentication and Firestore resources only after the replacement deployment is verified and an explicit retention decision is made. Source removal does not delete those remote resources.

## Sync and reporting contracts

Every semantic operation contains `id`, `entityId`, `type`, `expectedVersion`, and `payload`. An operation ID must retain exactly the same contents across retries. The server journals its payload hash and rejects reuse with altered data. A push transaction either commits all its operations or rolls back all of them.

SQL business locks serialize writes and change-feed reads, preventing cursor gaps caused by uncommitted transactions. Master edits use rowversion comparisons; stocks are adjusted through movements. Sales preserve entered selling-price snapshots; the server determines cost and stock effects when accepted. Offline projections are provisional until synchronization. Negative stock remains possible, matching the existing operational workflow.

IndexedDB compare-and-swap transactions preserve pending edits from concurrent tabs. Each owner/business pair has its own snapshot, cursor, outbox, and sync Web Lock. The `mashal-sql-v2` database namespace and operation serialization remain unchanged. Conflicts remain visible until reviewed; accepting server data requires confirmation before discarding pending device changes. Sign-out preserves account data and outbox records but hides them until that account signs in again.

## Owners, businesses, and Mashal Admin

The consolidated `001_InitialSchema.sql` creates support for multiple businesses per owner, unique normalized phone numbers, business requests, and the administrative audit trail. It does not seed users/businesses. Quantities retain `decimal(19,6)` precision.

Owners provide a Philippine mobile number during onboarding, submit a business request, and wait for approval. The server accepts `09`, `639`, or `+639` mobile notation (with common formatting), normalizes it to E.164, and enforces one account per number with a unique database index. This checks format and uniqueness, **not possession or reachability**. No SMS/OTP provider or charge is introduced; the UI never calls these numbers verified.

`/businesses` lists approved workspaces and request history. Only one pending request per owner is allowed. A rejected request requires a new submission; the original decision is retained. Approval creates an empty business and owner membership atomically. Clients cannot create businesses through sync. Each business must be opened online once before it is available offline on that device. The last selected business resumes on sign-in.

Configure `MashalAdmin:Emails` as an array in API configuration, or use environment variables `MashalAdmin__Emails__0`, `MashalAdmin__Emails__1`, etc. Set the exact Google email before starting the local API. No address is granted access by default. Google must report the email as verified. Sign in with an allowlisted account and open `/admin`. The deployment workflow maps `MASHAL_ADMIN_EMAILS` to these settings and refuses deployment without an administrator. Admins can review requests and see all owner/business metadata; admin status alone never grants access to sales, inventory, transactions, or financial reports.

Phone recovery requires the admin to complete an identity review outside the application, select the recipient, record a reason, and confirm the transfer. Transfers check the current holder and recipient's expected phone to reject stale decisions. The recipient's previous number is released; both numbers and the actors are retained in the audit. The former holder must add a different unique number before opening a business. Account settings and the business dashboard remain accessible. Requests, phone edits, approvals, and transfers are online-only. Cached offline records cannot be remotely revoked while a device is disconnected; the account is rechecked on reconnection before synchronization, and queued work is retained for recovery.

Financial endpoints require authentication and business membership. Summary, trends, product performance, and expense categories use inclusive business dates. Reports exclude unsubmitted device activity and are not cached by the service worker. Periods use Asia/Manila; trend weeks start Monday. Current-day device activity remains available offline, clearly labeled as device-local.

## External acceptance checks

Repository implementation and local tests do not configure external DNS, Google OAuth, MyASP.NET, or GitHub Environment secrets. Interactive Google sign-in and installed-device acceptance checks must be completed with those configured services before production cutover.
