# Mashal Systems Brand Instructions

## Official brand

Mashal Systems provides customized software applications for business and personal use. The brand should communicate structured systems, purposeful governance, progress, intelligence, trust, and clarity.

The name **Mashal** is based on the Hebrew root מָשַׁל (*māshal*), meaning to rule, govern, reign, or exercise dominion. Use this meaning as a subtle foundation for the brand; avoid overly literal religious imagery.

## Official color palette

- Deep Navy: `#0B1F3A` — primary navigation, headings, and authority
- Royal Red: `#A61B29` — primary calls to action, urgent actions, and decisive emphasis
- Royal Purple: `#5B2C83` — selected states, analytics, information, and secondary emphasis
- Gold: `#C99A2E` — logo details, decorative lines, and premium brand accents only
- Cloud White: `#F7F9FC` — application surfaces and backgrounds
- Slate Gray: `#536273` — secondary text and neutral UI elements

Use Deep Navy as the dominant color. Use Royal Red selectively, generally for the main call to action or urgent emphasis. Use Royal Purple for active states, information, and data visualization. Gold must remain decorative and must not communicate warnings, completion, progress, or other functional status. Maintain strong contrast and accessible typography.

## Semantic UI colors

- Success / completed: `#2E8B57`
- Warning: `#C58A00`
- Urgent / destructive: `#A61B29`
- Information / selected: `#5B2C83`

Do not use Gold as a warning or status color. Do not give multiple accent colors equal prominence within the same control group.

## Approved logo and export consistency

The approved business logo is the shielded M with a diamond above it, in Navy and Royal Red. This selected shield is an intentional exception to generic abstract-logo guidance. The source image is `scripts/brand-source/approved-reference.png`.

- Use `scripts/brand-source/shield-master.svg` as the single geometry master. Preserve mirrored shoulders, two lower panels, vertical center gap, and diamond; do not replace them with stacked chevrons.
- Reuse the approved SVG, PNG, and ICO exports in `public/brand`. Keep their geometry consistent with the master; do not draw individual icon versions independently or append another SVG root to a file.
- Use transparent gaps, not white or background-colored overlays. Monochrome includes the shield and diamond in the same ink.
- Export lettering as vector outlines. The generator uses bundled Montserrat fonts and license so exports have no font dependency.
- Logo colors are Navy and Royal Red, with Slate supporting type; reversed logos use Cloud White. Purple and Gold remain available in the wider UI palette, not in this logo.
- Rounded favicon tiles and opaque square app tiles use identical mark geometry and consistent safe padding. Review `public/brand/brand-preview.png` and actual-size icons after export.
- In the app, use `BrandLogo.razor` and the content-hashed files in `src/Mashal.BusinessAid.Client/wwwroot/brand-assets`. Preserve those URLs in HTML/manifest references and Firebase revalidation rules. Run `scripts/verify-release.ps1` after publishing to verify content hashes and offline inclusion.

## Visual direction

Favor clean, modern, premium SaaS interfaces with generous whitespace, clear information hierarchy, structured cards, tables, charts, task lists, and responsive layouts. Abstract geometric marks suggesting networks, structure, command, or progression are preferred. The visual identity should feel confident, premium, authoritative, and technology-focused. Avoid crowns, halos, crosses, church imagery, ornate religious styling, excessive gradients, and visual clutter.

Approved positioning line for concepts and mockups:

> Customized software for business and life.

## Maintainable .NET solution

- Use the root Mashal.BusinessAid.slnx: Client, API, Shared, Migrations, and Tests.
- Keep business rules in Shared, Dapper SQL and authorization in API, and browser UI/state in Client.
- Never introduce EF Core, FluentMigrator, MediatR, extra architecture layers, or frontend Node build dependencies.
- Preserve the IndexedDB namespace and serialized outbox format, and the DbUp logical script names.
- Verify both ordinary tests and the published PWA browser tests for offline/synchronization changes.
