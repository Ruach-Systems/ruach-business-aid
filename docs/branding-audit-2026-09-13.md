# Mashal Business Aid branding audit and release

Published to https://mashal-business-aid.web.app on 2026-09-13 (Asia/Singapore).

## Findings and changes

| Before | Updated |
| --- | --- |
| Forest green / cream global theme and green-tinted page styles | Navy structure, Cloud White surfaces, Slate supporting text |
| Letter M and dot used as a substitute logo | Approved outlined Mashal Systems logo shared by loading, sign-in, onboarding, desktop sidebar, and mobile header |
| Green primary actions and inconsistent selection styling | Royal Red primary actions; Royal Purple selected navigation, controls, and information |
| Gold used for completion actions, profit, warnings, and focus | Red actions, neutral financial totals, green success, amber warnings, purple focus; Gold retained only as an available decorative brand token |
| Serif headings and metrics | Consistent system sans-serif typography |
| Stable icon URLs that could retain older artwork | SHA-256 content-hashed filenames for all application branding images and manifest icons |

## Cache and deployment behavior

`build/branding.ts` reads the approved source assets and emits `/brand-assets/name.<content-hash>.ext`. Shared logo components, HTML icon links, and PWA manifest icons use these URLs. Byte changes produce new URLs automatically; unchanged artwork retains a stable URL. No timestamp updates or manual cache-version edits are required.

Firebase serves hashed assets with immutable caching. HTML (including SPA route fallbacks), the manifest, the worker, and stable legacy asset URLs require revalidation. Workbox precaches the hashed app images and retains the existing automatic update, online/focus checks, and old-cache cleanup. The downloadable brand catalog is excluded from the app precache. Offline devices receive updates when they reconnect; installed home-screen icon refresh timing is controlled by the browser/OS.

## Verification

- Production build and lint passed; 12 existing tests passed.
- Brand asset verification passed (geometry consistency, transparency, PNG dimensions, ICO pixels, and maskable padding).
- Release validation checked content hashes, HTML and manifest links, offline precache membership, and hosting cache rules.
- Desktop review at 1440 x 1000: dashboard, sales, inventory, products, production, expenses, and settings loaded without broken images or document overflow.
- Mobile review at 390 x 844: dashboard, inventory, stock receipt form, and More navigation; approved logo and red primary action verified.
- Authenticated page review used a separate local sample business with Firebase disabled. No production business records were created or changed for the audit.
- Hosting-only deployment completed. Live HTML, dashboard fallback, manifest, service worker, all ten versioned brand assets, and cache headers matched the local release byte-for-byte.
- An existing production browser tab first displayed its cached green-theme release, then automatically adopted the new branded release without clearing its cache. The loaded logo used the hashed URL and the primary button computed to Royal Red.
- The existing large JavaScript chunk warning remains; business logic and authentication behavior were not rewritten in this branding task.

Re-run release checks with `node scripts/verify-brand-release.mjs` after building, or pass the hosting URL to compare the live release.
