# Business Aid by RUACH identity source

**Status: agent-selected C / Balanced Record, owner review pending.**
The coordinator explicitly authorized reversible integration while the owner
was unavailable. This is not owner artwork approval. A / Open Ledger and
B / Stock Flow remain in [the concept comparison](concepts/index.html), alongside
C and the full-resolution comparison. `comparison-preview.png` records the
original pre-integration selection round; the current integrated draft is shown
in `public/brand/brand-preview.png`.

Balanced Record forms a B from paired record entries, linking the product name
to reliable records without positioning the whole app as stock-only. It does not
reuse the parent Living Breath mark or the other RUACH product emblems. The
parent brand repository was used read-only.

## Source and licenses

- `balanced-record-master.svg`: one editable integrated geometry master.
- `concepts/fonts/Manrope-800.ttf`: brand lettering.
- `concepts/fonts/Inter-400.ttf`: outlined endorsement.
- `concepts/fonts/Inter-Variable.ttf`: bundled product UI face.
- `concepts/fonts/*-OFL.txt`: retained upstream licenses.
- `scripts/brand-drawing.ps1`: shared vector font-outline helpers.
- `scripts/build-brand.ps1`: application/web export and hashing pipeline.

The parent font files were copied unchanged under their included OFL licenses.
All SVG lettering is outlined. Primary geometry is Crimson; reversed geometry
is White; one-color variants use Obsidian throughout. Gaps are actual negative
space, never background-colored patches.

## Rebuild on Windows

The asset authoring script uses Windows System.Drawing for bundled font outlines
and the repository's existing .NET Playwright/Chromium runtime for PNG rendering.
It does not add a frontend npm dependency or require Python. The normal .NET
application build consumes committed exports and does not run the authoring script.

```powershell
dotnet build tests\Ruach.BusinessAid.Tests
.\tests\Ruach.BusinessAid.Tests\bin\Debug\net10.0\playwright.ps1 install chromium
.\scripts\build-brand.ps1
.\branding\concepts\build-preview.ps1
dotnet publish src\Ruach.BusinessAid.Client -c Release -o artifacts\pwa -p:ApiOrigin=https://api.businessaid.ruachsystems.dev
.\scripts\verify-release.ps1
```

The builder regenerates actual web/PWA requirements in `public/brand`, copies
the required runtime assets (including the Inter license) into `wwwroot/brand-assets` with SHA-256 filename
suffixes and updates HTML, CSS, manifest and `BrandLogo.razor` references. It removes
obsolete files only from the dedicated generated runtime asset directory.
Rebuild twice to confirm deterministic hashes on the pinned authoring environment;
review any rendering-runtime upgrade before accepting new PNG hashes.

The 512 px opaque app tile places the 308 px emblem canvas at (102,102); its ink
fits within the central 80%-diameter maskable safe circle. Regular and maskable
icons intentionally share artwork. The browser ICO contains 16/32/48 px PNG
frames. Inspect the preview and actual-size icons after any geometry change.
Provisional minimum symbol size is 16 px; use at least 140 px for the endorsed
lockup and quarter-symbol clear space where layout permits.

Palette and UI decisions are recorded in `DESIGN.md`. Provider readiness,
secure settings, owner review and the no-deployment boundary are separate from
asset-generation checks.
