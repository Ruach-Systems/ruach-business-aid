# Business Aid by RUACH: staged domain cutover

Status: **preparation only; deployment prohibited by the current user instruction**.
Do not dispatch `deploy.yml`, deploy directly, change DNS/hosting or write to the
production database. A future explicit user instruction is required. Artwork
review is also pending: C / Balanced Record is agent-selected and integrated
under the coordinator's revised non-deploying direction, not owner-approved.

## Verified resources and remaining access

| Item | Verified or planned value |
| --- | --- |
| Destination repository | `Ruach-Systems/ruach-business-aid` (private) |
| Destination local checkout | `D:\WORK\MY APPS\My Business\ruach-business-aid` |
| Existing Firebase project | `mashal-business-aid` (real resource ID; retain) |
| Existing live PWA | `https://businessaid.mashalsystems.com` |
| Existing live API | `https://api-businessaid.mashalsystems.com` |
| Planned PWA | `https://businessaid.ruachsystems.dev` |
| Planned API | `https://api.businessaid.ruachsystems.dev` |

The coordinator verified the legacy PWA and API liveness/readiness return HTTP
200 with valid TLS. The **hyphenated** legacy API is authoritative. The obsolete
`api.businessaid.mashalsystems.com` hostname is NXDOMAIN. Both planned RUACH
hostnames were also NXDOMAIN at the 8 October 2026 inventory.

The destination Production environment contains `PWA_ORIGIN`, `API_ORIGIN` and
`FIREBASE_PROJECT_ID` with the planned values above. The coordinator also copied
`MASHAL_ADMIN_EMAILS` exactly from the source and verified it without printing
addresses. These four non-secret settings are configured; secret values remain
unconfigured. Its branch policy permits
only `main`. Required reviewers could not be enabled: GitHub returned HTTP 422
because the organization billing plan does not support that protection. Branch
filtering and a workflow checkbox do not substitute for owner authorization.

Securely provision the following secret **names**, never copy values into source
or logs: `DATA_PROTECTION_CERTIFICATE_BASE64`, `DATA_PROTECTION_CERTIFICATE_PASSWORD`,
`FIREBASE_SERVICE_ACCOUNT`, `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`,
`SQL_APP_CONNECTION_STRING`, `WEBDEPLOY_ENDPOINT`, `WEBDEPLOY_PASSWORD`,
`WEBDEPLOY_SITE`, `WEBDEPLOY_USERNAME`. GitHub cannot return stored secret values.

Provider access must establish the actual old and new Hosting site IDs, support
for a separate site within the existing Firebase project, MyASP.NET's additional
TLS bindings, DNS ownership and the existing Google OAuth client. No new site,
paid service or domain binding has been provisioned by this preparation.

## Explicit API/PWA pairs

`App:Origin` remains supported unchanged when `App:OriginPairs` is absent, so the
current single-origin configuration and development setup remain compatible.
Once any pair is configured, only listed API request scheme/host combinations
receive credentialed CORS or can initiate Google sign-in. Each API has exactly
one PWA origin. POST origin validation and OAuth success/failure destinations
use the same mapping; query-string return URLs are not accepted.

Set the nonsecret `APP_ORIGIN_PAIRS` GitHub Environment variable to this JSON
**only after provider bindings are confirmed**:

```json
[
  {
    "ApiOrigin": "https://api-businessaid.mashalsystems.com",
    "PwaOrigin": "https://businessaid.mashalsystems.com"
  },
  {
    "ApiOrigin": "https://api.businessaid.ruachsystems.dev",
    "PwaOrigin": "https://businessaid.ruachsystems.dev"
  }
]
```

The deployment script translates it into indexed `App__OriginPairs__0__ApiOrigin`
and `App__OriginPairs__0__PwaOrigin` settings (and subsequent indices). Configure
the same settings manually for a local host-based integration rehearsal.
Production accepts canonical HTTPS origins only, without credentials, paths,
queries, fragments, wildcard hosts or trailing slashes. Development alone allows
HTTP loopback origins.

Pairs are an explicit trust configuration, not a public-suffix detector. Operators
must verify that each PWA/API pair is same-site and HTTPS, that IIS preserves the
external host/scheme, and that untrusted forwarded headers are not enabled.
Do not add wildcard CORS, cookie domains spanning hosts, or `SameSite=None`.
Session `SameSite=Lax` and antiforgery `SameSite=Strict` remain intact.
Preserve the existing Google client/account mapping and register the new callback
`https://api.businessaid.ruachsystems.dev/api/auth/google/callback` in addition to
the old callback; do not remove it.

## Isolate PWA deployment

`firebase.json` now names the local target `business-aid`; `.firebaserc` keeps
the real project ID and intentionally has no guessed target-to-site mapping.
An authorized future workflow requires verified `FIREBASE_HOSTING_SITE` and
`FIREBASE_LEGACY_HOSTING_SITE` values that differ. It binds the target locally on
the runner, then uses `--only hosting:business-aid`, never generic Hosting deploy.
The new site must be separately verified in Firebase; a distinct string alone
does not prove that it is correctly bound or safe.

Keep the old PWA artifact compiled with the old API origin. Never deploy a
new-API artifact over the old site, or redirect old devices away before their
outboxes have drained. The new validation workflow compiles artifacts for the
RUACH API; this does not make the pending domains live.

## Device transition and rollback

1. On every old-origin device/business workspace, synchronize pending operations
   and resolve blocked items while the old PWA/API remain available.
2. Visit the new PWA online, sign in with the same Google account, and bootstrap
   existing server data. Reinstall the PWA on the new origin when needed.
3. Preserve old-origin access for recovery. IndexedDB and service workers are
   origin-scoped; cookies do not cross the new domain boundary. The server cannot
   prove that every offline device has drained its queue.

Never clear browser data or invent a cross-origin outbox import. Preserve
`mashal-sql-v2`, storage keys, serialized outbox, model version, SQL migration
identities, cookies and Data Protection application name/key material. This
rename requires no database schema migration.

Before an authorized release, retain compatible API/PWA artifacts and deployed
configuration, back up provider data/keys, validate target isolation and perform
live Google/account/business/role and offline-device acceptance. Roll back
artifacts/configuration, not the database after new business writes.

Coordinator evidence: imported-main validation run `37767569053` passed.
Successful original Production deployment run `37735851746` used
`acbaf96dc519dd396726fbcdddfd2e47384efcdc`; its API/PWA artifacts were retained at
`C:\Users\Z\.copilot\session-state\ac563725-48ea-4890-bf30-f75bd9769d48\files\rollback-baseline`.
The original release verifier passed against both the saved PWA and live old
origin. The API artifact is **pre-secret injection**, not a backup of deployed
secrets, Data Protection keys or the database. These are baseline evidence,
not proof that a future RUACH release has passed acceptance.

## Destination validation and current handoff

The destination Release build passed with no warnings/errors. Local verification
passed 79 ordinary tests, 14 SQL/HTTP integration tests, and all 6 published-PWA
browser tests. Integration used only the newly created
`RuachRebrandTests_acb2e68aad0d4c1ab63db3dd07308a3c` database on
`.\SQLDEVELOPER2025`; all four existing DbUp migrations applied, no pending
migrations remained, and that database was removed afterward.

New tests cover exact old/new API pair CORS, Google callback/return destinations,
foreign/cross-pair origin rejection, unchanged POST antiforgery enforcement,
content-hashed identity references, outlined SVG master reuse and ICO sizes.
The browser addition covers independent-origin bootstrap without transferring
or deleting the old origin's pending outbox, offline reload, 320/1366 px layouts,
loaded logos and opaque maskable-icon safe-circle pixels. These use local test
hosts and fixture Google/account data, **not live Google OAuth acceptance**.

The non-deploying PowerShell origin-settings tests and script syntax checks
passed. PWA publish and `scripts/verify-release.ps1` passed; the portable API
publish retained the `dotnet .\Mashal.BusinessAid.Api.dll` launch configuration.
Repeated asset export produced identical checksums. The bounded design scan
reported only the intentional, parent-required Inter font choice.

Review `public/brand/brand-preview.png` and the retained alternatives in
`branding/concepts`. Local runtime screenshots are in
`artifacts/browser/ruach-dashboard-320.png` and
`artifacts/browser/ruach-dashboard-1366.png`. Artwork is agent-selected; owner
review, provider bindings, secure provisioning and live/device acceptance remain
pending. No deployment workflow, direct deployment, production data/schema
change, old-origin redirect or source-repository write was performed.
