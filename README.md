# MASHAL — Business Aid

MASHAL is an installable, offline-first PWA for a small food business. It turns daily quantities into sales, product cost, expenses, estimated profit, and auditable stock movements without becoming a full POS or accounting system.

## Included in V1

- Google sign-in with one Google account mapped to exactly one business
- Local-first use with installable PWA caching
- Automatic app updates at startup, on reconnect, and while the app remains open
- Firestore cloud sync and multi-device live updates
- Products, prices, manual costing, and ingredient recipes
- Made-when-sold, prepared-in-advance, and untracked product modes
- Fast daily sales entry with optional inventory deduction
- Inventory items, low-stock levels, receipts, and movement history
- Weighted-average stock costing
- Audited stock adjustments and waste recording
- Draft and completed production batches with actual ingredient use and yield
- Daily operating expenses with edit and delete
- Dashboard sales, cost, expense, profit, items sold, and low-stock totals

## Stack

- Ionic Vue 9 + Vue 3 + TypeScript
- Vite + `vite-plugin-pwa`
- Pinia
- Firebase Authentication and Cloud Firestore
- Firebase Hosting configuration
- Vitest

## Run locally

```bash
pnpm install
pnpm dev
```

Without Firebase configuration, the sign-in page offers **Continue locally**. This exercises the whole application with browser-local persistence and optional sample data.

## Enable Google sign-in and cloud sync

1. Create a project on the Firebase Spark plan.
2. Add a Web App in Firebase project settings.
3. Enable **Authentication → Sign-in method → Google**.
4. Create a Cloud Firestore database.
5. Copy `.env.example` to `.env` and fill in the Web App values:

```dotenv
VITE_FIREBASE_API_KEY=
VITE_FIREBASE_AUTH_DOMAIN=
VITE_FIREBASE_PROJECT_ID=
VITE_FIREBASE_STORAGE_BUCKET=
VITE_FIREBASE_MESSAGING_SENDER_ID=
VITE_FIREBASE_APP_ID=
VITE_FIREBASE_MEASUREMENT_ID=
```

6. Deploy the included Firestore rules and indexes before using production data.

The authenticated Firebase UID is also the business document ID. All operational records live beneath `businesses/{uid}`. The included rules reject access from any other account and prevent changing the owner UID.

## Build and verify

```bash
pnpm test
pnpm build
pnpm preview
```

The generated `dist/` directory includes the web manifest, automatic service-worker registration, and precached application shell.

Published updates replace only the cached application shell. Business data remains in the stable `mashal:v1:{uid}` local-storage record and Firestore's separate offline cache. The client checks for updates when it starts, reconnects, returns to the foreground, and every 15 minutes while open; a ready update activates and refreshes automatically.

## Firebase Hosting

The repository includes `firebase.json`, `firestore.rules`, and `firestore.indexes.json`. After selecting your Firebase project:

```bash
pnpm build
pnpm dlx firebase-tools deploy --only hosting,firestore:rules,firestore:indexes
```

Add the deployed hosting domain to Firebase Authentication's authorized domains if it is not added automatically.

## Important business rules

- Money is stored as integer centavos.
- Stock balances are updated only alongside append-only inventory movements.
- Stock receipts recalculate weighted-average cost.
- A sale snapshots its selling price and product cost.
- Batch drafts do not change inventory.
- Completing a batch writes ingredient-out and finished-stock-in changes together.
- Completed batches remain immutable; corrections use an inventory adjustment.
