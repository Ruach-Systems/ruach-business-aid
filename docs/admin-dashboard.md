# Platform admin dashboard

The `/admin` desktop sidebar and mobile header/bottom navigation implement the approved solid, light RUACH concept: pending requests first, platform counts, financial cards, revenue/net-profit chart, and top businesses. Mobile cards form two columns with a full-width net-profit card. More provides the existing audit trail and account navigation.

## Scope and behavior

- Overview shows the five oldest pending requests. Review retains the existing approve/reject confirmation, 1,000-character rejection reason, phone prerequisite, empty-workspace provisioning, and decision audit.
- Overall user/business counts are independent of the financial period. New-user/business counts follow the selected period. Dates use Asia/Manila; This month is the default. Today, This week (Monday start), Last 30 days, and inclusive custom dates are available.
- Financial values use synchronized records, PHP, integer centavos, and snapshotted sale costs. Revenue minus item cost is gross profit; gross profit minus expenses is net profit. Deleted records are excluded, expense-only dates appear, and negative profit is retained. Stock purchases are not counted again as expenses.
- The chart uses actual date positions, a zero baseline and common scale. Expand its table for exact amounts. Top five businesses are ordered by revenue, then name and ID.
- Users and businesses have server-side search and stable pagination (25 rows in the UI; API limit 100). Business directory cards show this month's financial totals. Detail pages have their own date selector.
- Business inspection includes item performance, expenses by category, date-filtered itemized sales, expenses, current inventory/low stock, stock receipts, adjustments, and members. Inventory is current and explicitly independent of date filters. Sales use their business date; receipts/adjustments use Manila dates derived from their timestamps.
- User inspection includes profile, owned businesses, memberships, requests, and combined owned-business financial totals. These are ownership totals, not attribution to individual actions.
- No sign-in history, last-seen status, activity feed, retention job, or new tracking table is added. The existing latest-200 admin decision/phone-transfer audit remains available.

## Access and data boundaries

Every new query calls `AccountService.RequireAdmin`, which rechecks the verified-email allowlist. Routes require authentication. `AdminQueries` is read-only and separate from normal membership-guarded reports and sync endpoints. `ReportQueries.Read` shares report SQL internally, after the respective caller's authorization. No workspace membership or operational write privilege is granted to admins.

New routes under `/api/admin`: `analytics`, `directory/{users|businesses}`, `businesses/{id}/members`, `users/{id}/memberships`, `businesses/{id}/records/{sales|expenses|items|receipts|adjustments}`, and `businesses/{id}/reports/{report}`. Records are always scoped by business, including sale lines. The existing `/api/admin` metadata response remains for request decisions, owner lookup, and phone transfer compatibility.

Admin data lives only in component memory; it never enters `BusinessState.Data`, IndexedDB, the outbox, or the service-worker cache. API requests and responses retain `no-store`. Viewing a business does not switch the selected working business. Offline financial/record views require reconnection.

No schema migrations, hosting, domains, database configuration, or deployment workflow changes are required.

## Local verification

Use the normal Release build, ordinary test suite, isolated SQL/HTTP integration suite, published-PWA browser suite, and `scripts/verify-release.ps1`. SQL tests must use a uniquely named disposable local database. New coverage checks aggregate accuracy, deleted/out-of-range expenses, expense-only negative profit, ownership scope, pagination, snapshot lines, admin authorization, unchanged workspace restrictions, no-store HTTP responses, responsive layout, date validation, and absence of admin operational records in IndexedDB.

Browser screenshots are written to ignored `artifacts/browser/admin-overview-{320,390,768,1366}.png`, `admin-business-mobile.png`, and the existing admin review/transfer screenshots. They use synthetic test data, not production records.

Verified locally on 2026-10-22: Release build with zero warnings/errors; 81 ordinary tests, 20 isolated SQL/HTTP tests, and 10 published-PWA browser tests passed. Focused admin browser tests were rerun after final visual/accessibility refinements. Release asset/manifest verification and `git diff --check` passed. The disposable local SQL database was removed after testing. No production migration or deployment was performed.
