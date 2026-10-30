# Application alerts

CoolAlertJS 1.0.4 is bundled under `wwwroot/vendor/coolalert/`. The package retains its upstream ISC license header; package metadata is stored beside it. It is included in the published service-worker asset manifest. No CDN or runtime package request is used.

Use `businessAlerts.confirm`, `success`, `error`, or `warning` through JS interop. Confirmation returns true only on explicit confirmation. Dialogs are queued, restore focus, constrain keyboard focus, and default to Cancel. Pass plain text: the adapter escapes it before passing it to the vendor's HTML templates. Success notifications dismiss after five seconds.

`AppNotification` presents changed messages; `ErrorBanner` delegates shared operation errors to it. Sales, expenses, stock, item saves, account updates, requests, and admin actions use this shared integration. Settings reset, sync conflict resolution, expense deletion, approvals/rejections, and phone transfers use confirmations. Inline field validation, persistent offline/stock status and the startup-failure fallback remain part of the page.

Opaque RUACH theme text contrast (WCAG relative luminance):
- White on Crimson: 6.70:1.
- Aubergine on Aubergine Tint: 12.81:1.
- Graphite on White: 14.98:1.
- Slate on White: 5.86:1.
- White on warning brown: 6.79:1.
- White on Success Green: 6.26:1.

These exceed AA's 4.5:1 normal-text threshold. This is a contrast and focused keyboard verification, not a whole-application accessibility certification.
