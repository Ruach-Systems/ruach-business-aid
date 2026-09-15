import fs from 'node:fs/promises'
import vm from 'node:vm'
import assert from 'node:assert/strict'

// Inspect the generated worker's actual route options, not just the source config.
const worker = await fs.readFile(new URL('../dist/sw.js', import.meta.url), 'utf8')
const routes = []
const workbox = {
  clientsClaim() {}, precacheAndRoute() {}, cleanupOutdatedCaches() {},
  createHandlerBoundToURL: url => url,
  registerRoute: route => routes.push(route),
  NavigationRoute: class { constructor(handler, options) { this.handler = handler; this.options = options } }
}
vm.runInNewContext(worker, { self: { define() {}, skipWaiting() {} }, define: (_dependencies, factory) => factory(workbox) })
const route = routes.find(route => route.handler === '/index.html')
assert(route, 'Offline app navigation must remain available')
for (const url of ['/__/auth/handler', '/__/auth/handler?state=test', '/__/auth/iframe', '/__/firebase/init.json']) {
  assert(route.options.denylist.some(pattern => pattern.test(url)), `Worker would intercept Firebase: ${url}`)
}
for (const url of ['/sign-in', '/dashboard', '/inventory', '/sales']) {
  assert(!route.options.denylist.some(pattern => pattern.test(url)), `Offline app route excluded: ${url}`)
}
console.log('PASS: generated worker bypasses Firebase helpers while preserving offline app navigation.')

if (process.argv[2]) {
  const origin = new URL(process.argv[2]).origin
  const liveWorker = await fetch(`${origin}/sw.js`, { cache: 'no-store' })
  assert.equal(await liveWorker.text(), worker, 'Live worker differs from the verified build')
  for (const helper of ['handler', 'iframe']) {
    const response = await fetch(`${origin}/__/auth/${helper}`, { cache: 'no-store' })
    assert.equal(response.status, 200)
    const html = await response.text()
    assert(html.includes(`${helper}.js`), `Firebase ${helper} is not being served`)
    assert(!html.includes('id="app"'), 'App rewrite intercepted the Firebase helper')
  }
  console.log(`PASS: live worker and Firebase sign-in helper endpoints at ${origin}`)
}
