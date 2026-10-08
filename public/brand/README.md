# Business Aid by RUACH assets

**Agent-selected Balanced Record; owner artwork review pending. Not deployed.**

These generated assets derive from `branding/balanced-record-master.svg`.
See [the source kit instructions](../../branding/README.md) for provenance,
licenses, rebuild requirements and the retained alternative concepts.

| Use | Asset |
| --- | --- |
| Primary / reverse / single-ink lockups | `business-aid-wordmark*.svg` |
| Emblem / single-ink emblem | `business-aid-symbol*.svg` |
| Browser favicon | `favicon.svg`, `favicon.ico` (16/32/48 px) |
| Apple touch icon | `apple-touch-icon.png` (180 px) |
| Regular PWA icon | `pwa-192x192.png`, `pwa-512x512.png` |
| Maskable PWA icon | `pwa-maskable-512x512.png` |
| Review board | `brand-preview.svg`, `brand-preview.png` |

The app tile is opaque Crimson, with the white emblem inside the maskable safe
circle. The favicon uses the same geometry/padding in a rounded container.
Wordmarks use outlined Manrope 800 and Inter 400; no installed font is required
to display an SVG. UI fonts are bundled separately in hashed runtime assets.

`runtime-assets.json` maps logical files to the generated content-hashed URLs.
`asset-inventory.json` records checksums. Do not edit generated SVG/PNG/ICO assets
independently; edit the master and regenerate. A successful export or test is not
owner artwork approval, live OAuth acceptance or production authorization.
