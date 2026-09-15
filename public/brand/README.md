# Mashal Systems brand assets

These assets reproduce the approved shielded M and diamond from `scripts/brand-source/approved-reference.png`. All variants share one vector master, true transparent gaps, and outlined lettering. The preview board shows actual exports, not an AI mockup.

## Choosing a file

| Use | SVG master | Raster exports |
| --- | --- | --- |
| Standalone color mark | mashal-icon.svg | 256, 512, 1024 px PNG |
| Single navy ink, including diamond | mashal-monochrome.svg | 256, 512, 1024 px PNG |
| Solid black / solid white mark | mashal-black.svg / mashal-white.svg | 256, 512, 1024 px PNG |
| White mark with red diamond, transparent | mashal-reversed.svg | 256, 512, 1024 px PNG |
| Horizontal business logo | mashal-wordmark.svg | 1520 x 512, 3040 x 1024 PNG |
| Horizontal single-ink logo | mashal-wordmark-monochrome.svg | Same horizontal sizes |
| Horizontal reversed logo, transparent | mashal-wordmark-reversed.svg | Same horizontal sizes |
| Horizontal logo on solid navy | mashal-dark.svg | Same horizontal sizes |
| Stacked business logo | mashal-stacked.svg | 1280 px PNG |
| Stacked logo on navy, as in sample | mashal-stacked-dark.svg | 1280 px PNG |
| Stacked single-ink logo | mashal-stacked-monochrome.svg | 1280 px PNG |
| Lettering without icon | mashal-logotype.svg | 1360 x 460 PNG |
| Rounded browser / desktop tile | mashal-favicon.svg | 16, 24, 32, 48, 64, 128, 180, 192, 256, 512 px PNG |
| Opaque square app tile | mashal-app-icon.svg | 48, 72, 96, 144, 180, 192, 256, 512, 1024 px PNG |
| Browser / Windows icon | mashal-favicon.ico / mashal-desktop.ico | Each embeds 16, 24, 32, 48, 64, 128, 256 px |

Use SVG for business stationery, signage, design tools, and scalable web placements. PNG is RGB raster artwork; use its native dimensions or smaller. SVG text is outlined, so a printer or another computer does not need the font installed. Black and white versions are genuinely single-ink, including the diamond.

The square app tile is fully opaque and leaves room for platform masking. The rounded favicon is a separate container treatment of the same mark. Root browser, Apple touch, and PWA files are generated from these sources too. Do not manually stretch or redraw any variant.

## Rebuild and verify

Run `pnpm brand:assets`, then `node scripts/verify-brand-assets.mjs` from the project. The source geometry is `scripts/brand-source/shield-master.svg`. Bundled Montserrat ExtraBold and Bold are used for outlined lettering; see the bundled `OFL.txt` and [font source](https://github.com/JulietaUla/Montserrat). Font files are only build inputs.

Review `brand-preview.png` for the color, horizontal, monochrome, stacked dark, and favicon treatments. `asset-inventory.json` lists exported PNG dimensions and ICO sizes.

Brand colors: Deep Navy `#0B1F3A`, Royal Red `#A61B29`, Cloud White `#F7F9FC`, Slate Gray `#536273`. Purple and gold do not appear in this logo system.
