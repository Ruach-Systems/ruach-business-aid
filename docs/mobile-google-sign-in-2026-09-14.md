# Mobile Google sign-in repair

## Evidence

The supplied Android recording shows Google consent on the firebaseapp.com helper followed by a return to web.app and the sign-in screen. The configured authDomain is mashal-business-aid.firebaseapp.com, while the published app uses mashal-business-aid.web.app. Mobile and narrow windows previously always used signInWithRedirect.

Firebase documents that third-party storage partitioning breaks this cross-origin redirect handoff. Desktop used signInWithPopup, which explains the different behavior.

A Google OAuth preflight for the web.app /__/auth/handler URI returned redirect_uri_mismatch. Do not simply switch authDomain without first configuring and verifying the OAuth redirect URI. No Google OAuth settings were changed in this repair.

## Changes

- Use popup sign-in whenever the app hostname differs from the configured authDomain, including phones and installed PWAs. Keep mobile redirects only on the same hostname as the helper.
- Never fall back to a cross-origin redirect when a popup is blocked. Explain how to allow popups/open the app directly in Chrome or Safari instead.
- Do not redirect on cancelled-popup-request, which can indicate another sign-in is already open.
- Exclude Firebase reserved /__/ URLs from the service worker's app-shell navigation fallback.
- Preserve local business storage, Firestore configuration, branding hashes, and automatic shell updates. No site-data clearing or origin migration is needed.

## Verification

Run pnpm run test, pnpm run lint, pnpm run build, node scripts/verify-auth-release.mjs, and node scripts/verify-brand-release.mjs. Both release verifiers accept the production origin for post-deployment checks.

Unit and mocked SDK integration tests cover Android, iPhone, installed PWA, desktop, same-origin redirect, popup completion, errors, and redirect-result consumption. A physical-phone Google account round trip still requires user retesting; a desktop viewport is not a substitute for Android/iOS browser and installed-PWA behavior.

All 29 tests, lint, TypeScript checking, production build, auth release checks, and branding release checks passed locally. Hosting deployment was attempted but stopped because the existing project account's Firebase CLI credentials expired. The other available CLI account does not list this project. Production is unchanged pending reauthentication and successful hosting deployment.

## Reference

[Firebase redirect sign-in best practices, option 2](https://firebase.google.com/docs/auth/web/redirect-best-practices#option_2_switch_to_signinwithpopup).
