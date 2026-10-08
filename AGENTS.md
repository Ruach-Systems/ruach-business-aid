# Business Aid by RUACH

## Product identity

The public name is **Business Aid by RUACH**. RUACH is the parent identity;
Ruach Software Development Services is the legal business identity.
The read-only parent authority is `D:\WORK\MY APPS\My Business\ruach-brand-guide`.
Do not write to that guide or its product catalog from this repository.

**Balanced Record (concept C) is agent-selected; owner artwork review is pending.**
The coordinator authorized reversible, non-deploying integration after the owner
was unavailable. Do not describe this artwork as owner-approved. Retain the
alternatives in `branding/concepts` and use `branding/balanced-record-master.svg`
as the single geometry source for the integrated emblem.

Use Crimson `#B21F32`, Obsidian `#141821`, Royal Aubergine `#321C3B`, Ivory
`#F7F3ED`, Graphite `#292530`, Slate `#696270` and White. Crimson is the primary
action, Aubergine the selected/information state, and Obsidian the navigation
surface. Success uses `#2E6B57`; warnings retain distinct amber semantics.
Never communicate functional state with a decorative gold accent.

Manrope is the brand/wordmark face; Inter is product typography. Bundled font
licenses are in `branding/concepts/fonts`. Lettering in SVG logos must be
outlined, all gaps transparent, and all variants derived from one master.
Do not reuse or alter RUACH Living Breath, Steward Royal Exchange, ChordKeep
Royal Folio or the superseded Mashal shield.

Use `BrandLogo.razor` and content-hashed assets in the Client `wwwroot/brand-assets`
directory. Rebuild with `scripts/build-brand.ps1` as documented in
`branding/README.md`; it updates hashed references coherently. Run
`scripts/verify-release.ps1` after publishing. Keep Firebase immutable caching
for hashed assets and revalidation for HTML, manifests and service workers.

Preserve the established layout, responsive patterns, interactions, hierarchy,
forms and semantic states. Do not introduce a dark-mode feature.

## Compatibility and architecture

- Use `Mashal.BusinessAid.slnx`: Client, API, Shared, Migrations and Tests.
- Keep business rules in Shared, Dapper SQL and authorization in API, browser
  UI/state in Client. Retain .NET 10, ASP.NET Core, Blazor and SQL Server.
- Never introduce EF Core, FluentMigrator, MediatR, Supabase, extra architecture
  layers or frontend Node build dependencies.
- Retain solution/project/assembly/namespace names, configuration keys, cookie
  names, Data Protection application identity and keys, DbUp logical script
  names, immutable SQL migrations, IndexedDB `mashal-sql-v2`, browser storage
  keys, serialized outbox and model version.
- Legacy `--mashal-*` CSS custom-property names remain private compatibility
  aliases with RUACH values; they are not the public brand.
- For offline/synchronization changes verify ordinary tests and the existing
  published-PWA browser suite. SQL/HTTP integration tests require a uniquely
  named dedicated test database, never an existing or production database.

## Deployment boundary

**Do not trigger a deployment workflow, deploy directly or perform production
cutover under the current user instruction.** Non-deploying validation is allowed.
See `docs/domain-cutover.md` for verified resource IDs, pending provider/secret
configuration, required future authorization and old-origin recovery.

Keep the original repository, checkout, remote configuration and live origins
untouched. Never clear browser storage or automatically migrate outboxes across
origins. New-origin sign-in/bootstrap does not prove old devices have synchronized.
