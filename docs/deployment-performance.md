# Deployment runtime and minute usage

The deployment workflow validates the selected environment/API origin and database
confirmation before starting build validation. Production deployment remains serialized
and is never automatically cancelled by a newer dispatch.

Validation still runs the full ordinary, SQL/API integration, published-PWA browser,
and migration checks. NuGet packages are cached by OS and project configuration;
restore still runs, while both publish steps use `--no-restore`. Browser test hosts
log Warning and above to reduce log traffic. Chromium installation downloads only
the headless shell; CI does not set a branded-browser channel. Ubuntu package
requests have timeouts and installation has two four-minute attempts. A failed
installation still blocks the release.

Firebase CLI npm downloads are cached. Web Deploy installation is skipped when
its expected executable already exists. Job timeouts bound unexpected stalls;
they do not establish that a release passed.

When a deployment fails after validation succeeds, use GitHub's **Re-run failed
jobs** for the same commit/run instead of dispatching a new full deployment.
This retains the successful validation job and its artifacts, subject to artifact
retention. A new commit must pass validation again. Never bypass a failed test.

Reference successful run 42782645018: validation 9m46s, browser installation 1m41s,
browser tests 5m18s, API deployment 1m09s, PWA deployment 43s. These are elapsed
runner timings, not a billing calculation. Cache benefits require a warm run;
new timings and billed-minute savings have not yet been measured.
