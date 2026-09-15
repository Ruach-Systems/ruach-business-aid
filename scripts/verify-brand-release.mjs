import fs from 'node:fs/promises'
import path from 'node:path'
import { createHash } from 'node:crypto'
import assert from 'node:assert/strict'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('../', import.meta.url))
const dist = path.join(root, 'dist')
const html = await fs.readFile(path.join(dist, 'index.html'), 'utf8')
const manifest = JSON.parse(await fs.readFile(path.join(dist, 'manifest.webmanifest'), 'utf8'))
const worker = await fs.readFile(path.join(dist, 'sw.js'), 'utf8')
assert(!html.includes('%BRAND_'))
for (const name of await fs.readdir(path.join(dist, 'brand-assets'))) {
  const data = await fs.readFile(path.join(dist, 'brand-assets', name))
  const hash = createHash('sha256').update(data).digest('hex').slice(0, 16)
  assert(name.includes(`.${hash}.`), `Incorrect content hash: ${name}`)
  assert(worker.includes(`brand-assets/${name}`), `Missing offline cache entry: ${name}`)
}
const links = [...html.matchAll(/href="([^"]+)"/g)].map(match => match[1]).filter(url => /\.(svg|ico|png)$/.test(url))
assert.equal(links.length, 3)
for (const url of [...links, ...manifest.icons.map(icon => icon.src)]) {
  assert(/^\/brand-assets\/.+\.[a-f0-9]{16}\.(svg|ico|png)$/.test(url), `Unversioned image: ${url}`)
  await fs.access(path.join(dist, url))
}
assert(!worker.includes('brand/brand-preview'), 'Brand catalog should not be precached by the app')
const config = JSON.parse(await fs.readFile(path.join(root, 'firebase.json'), 'utf8'))
assert(config.hosting.headers.some(rule => rule.source === '**' && rule.headers.some(h => h.value.includes('must-revalidate'))))
assert(config.hosting.headers.some(rule => rule.source === '/brand-assets/**' && rule.headers.some(h => h.value.includes('immutable'))))
console.log('PASS: content hashes, icon references, offline precache entries, and Firebase cache rules.')

if (process.argv[2]) {
  const origin = new URL(process.argv[2]).origin
  for (const route of ['/index.html', '/manifest.webmanifest', '/sw.js', '/dashboard']) {
    const response = await fetch(origin + route, { cache: 'no-store' })
    assert.equal(response.status, 200, route)
    assert(response.headers.get('cache-control')?.includes('must-revalidate'), route)
    const expected = route === '/sw.js' ? worker : route === '/manifest.webmanifest' ? await fs.readFile(path.join(dist, 'manifest.webmanifest'), 'utf8') : html
    assert.equal(await response.text(), expected, `Deployed content differs: ${route}`)
  }
  for (const name of await fs.readdir(path.join(dist, 'brand-assets'))) {
    const response = await fetch(`${origin}/brand-assets/${name}`, { cache: 'no-store' })
    assert.equal(response.status, 200, name)
    assert(response.headers.get('cache-control')?.includes('immutable'), name)
    const live = Buffer.from(await response.arrayBuffer())
    assert(live.equals(await fs.readFile(path.join(dist, 'brand-assets', name))), `Deployed image differs: ${name}`)
  }
  console.log(`PASS: live HTML, route fallback, manifest, service worker, all 10 brand assets, and cache headers at ${origin}`)
}
