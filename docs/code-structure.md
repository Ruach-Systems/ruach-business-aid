# Code structure

Open `Ruach.BusinessAid.slnx`. There are five projects: Client, API, Shared, Migrations and Tests. Folder names organize code inside those existing projects; they do not add architectural layers.

## Where to work

| Change | Location |
|---|---|
| A screen, form or page-specific styling | `src/Ruach.BusinessAid.Client/Pages/<feature>` |
| A reusable UI element | Client `Components` |
| Navigation and page shells | Client `App.razor` and `Layout` |
| Sign-in, business selection or account state | Client `Services/BusinessState.Accounts.cs` |
| Device edits and queued operations | Client `Services/BusinessState.Operations.cs` |
| Sync coordination and conflict review | Client `Services/BusinessState.Synchronization.cs` |
| IndexedDB persistence and synchronization protocol | Client `Services/OfflineStorage.cs`, `OfflineState.cs` and `SyncService.cs` |
| API dependencies, cookies or Google authentication | API `Configuration/ApiServices.cs` |
| HTTP error handling, origin checks or CSRF protection | API `Middleware/ApiMiddleware.cs` |
| HTTP routes and request/response mapping | API `Endpoints/ApiEndpoints.cs` |
| SQL queries, authorization or database transactions | API `Data` |
| Business records | Shared `Models` |
| Request/response types and JSON settings | Shared `Contracts` |
| Calculations, validation or operation processing | Shared `Rules` |
| Local projections and the in-memory repository | Shared `Offline` |
| A database schema change | `database/migrations` and `tools/Ruach.BusinessAid.Migrations` |
| Regression coverage | `tests/Ruach.BusinessAid.Tests` |

## Client pages

Pages are grouped into Accounts, Dashboard, Items, Sales, Expenses and Reports. The historical `Pages/Inventory` folder still contains the Item components; its public routes are `/items`, while old Inventory, Products, and Production URLs redirect to Items. Keep a page's `.razor`, `.razor.cs` and `.razor.css` files together. Existing page class names remain in `Ruach.BusinessAid.Client.Pages`; explicit Razor namespaces keep code-behind and component references stable. Routes are declared by `@page`, not derived from folders.

`BusinessState.cs` declares the shared state, dependencies and change notifications. Its three partial files are parts of the **same class**, using the same synchronization gate. They are not independently registered services. Keep the order of persistence, notification, locking and synchronization operations intact when editing them.

## API and shared rules

API `Program.cs` reads the public origin, registers services, builds the app, installs middleware, maps routes and runs the server. The extracted setup methods retain that order. Endpoints delegate to the existing data services; business authorization and transaction boundaries stay there.

Shared code keeps the `Ruach.BusinessAid.Shared` namespace. Both server transactions and provisional device projections use the same command processor. Preserve the simplified operation names, JSON field names, centavo money calculations, and whole-number quantity rules when reorganizing files.

## Database and release workflow

DbUp stays in its standalone console project. SQL files stay under `database/migrations` and retain the embedded logical names `Mashal.Migrations.<filename>`. Keep 001 and 003 immutable. Migration 004 expands to the empty simplified operational model; 005 contracts the legacy model only after application/reset verification. Once a migration is applied, add a forward correction rather than editing it. The API does not run migrations on startup.

The existing validation and deployment workflows continue to apply migrations, check the journal and enforce the production backup preflight. See the root README for configuration and commands.

## Compatibility and verification

Preserve the `mashal-sql-v2` IndexedDB namespace, storage keys, cookies, authorization, and offline retry behavior. The saved workspace, bootstrap, pull, and push contracts are model-versioned: incompatible operational snapshots and outboxes must be reset rather than interpreted. Keep brand assets and accessible UI behavior stable when making structural changes.

Run ordinary tests after code changes. For API or SQL changes, also run integration tests against a dedicated migrated test database. For page, state or offline changes, publish the PWA, run the browser tests and `scripts/verify-release.ps1`. The root README contains the exact commands. Live Google sign-in and installed-device acceptance remain separate from the fixture-based browser suite.
