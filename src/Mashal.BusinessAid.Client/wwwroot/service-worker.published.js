self.importScripts('./service-worker-assets.js');
const prefix = 'mashal-blazor-';
const cacheName = prefix + self.assetsManifest.version;
const assets = self.assetsManifest.assets.filter(x => !/^(api|health)\//.test(x.url) && !x.url.includes('service-worker') && x.url !== 'sw.js' && !x.url.endsWith('.gz') && !x.url.endsWith('.br'));
self.addEventListener('install', event => event.waitUntil((async () => {
  const cache = await caches.open(cacheName);
  // Fail installation on a partial or mismatched release; the previous app remains usable.
  await cache.addAll(assets.map(asset => new Request(asset.url, { integrity: asset.hash, cache: 'no-cache' })));
  await self.skipWaiting();
})()));
self.addEventListener('activate', event => event.waitUntil((async () => {
  // Cache Storage contains app shells only. Never touch IndexedDB or another app's storage.
  for (const name of await caches.keys()) if (name !== cacheName && name.startsWith(prefix)) await caches.delete(name);
  await self.clients.claim();
})()));
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if (event.request.method !== 'GET' || url.origin !== self.location.origin || /^\/(api|health)(\/|$)/.test(url.pathname)) return;
  event.respondWith((async () => {
    const cache = await caches.open(cacheName);
    const key = event.request.mode === 'navigate'
      ? (/^\/open\/?$/.test(url.pathname) ? 'open.html' : 'index.html')
      : event.request;
    return await cache.match(key) || fetch(event.request);
  })());
});
