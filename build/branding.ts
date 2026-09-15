import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import type { Plugin } from 'vite'

export function assetFileName(name: string, bytes: Uint8Array): string {
  const hash = createHash('sha256').update(bytes).digest('hex').slice(0, 16)
  const dot = name.lastIndexOf('.')
  return `brand-assets/${name.slice(0, dot)}.${hash}${name.slice(dot)}`
}

export function branding() {
  const sourceNames = {
    logo: 'brand/mashal-wordmark.svg', reversedLogo: 'brand/mashal-wordmark-reversed.svg',
    icon: 'brand/mashal-icon.svg', reversedIcon: 'brand/mashal-reversed.svg',
    favicon: 'favicon.svg', ico: 'favicon.ico', apple: 'apple-touch-icon.png',
    pwa192: 'pwa-192x192.png', pwa512: 'pwa-512x512.png', maskable: 'pwa-maskable-512x512.png'
  }
  const entries = Object.entries(sourceNames).map(([key, source]) => {
    const bytes = readFileSync(fileURLToPath(new URL(`../public/${source}`, import.meta.url)))
    const name = assetFileName(source.split('/').pop()!, bytes)
    return { key, bytes, name, url: `/${name}` }
  })
  const urls = Object.fromEntries(entries.map(entry => [entry.key, entry.url])) as Record<keyof typeof sourceNames, string>
  const plugin: Plugin = {
    name: 'mashal-brand-assets',
    resolveId(id) { if (id === 'virtual:mashal-brand') return '\0virtual:mashal-brand' },
    load(id) { if (id === '\0virtual:mashal-brand') return `export default ${JSON.stringify(urls)}` },
    transformIndexHtml(html) {
      return html.replaceAll('%BRAND_FAVICON%', urls.favicon).replaceAll('%BRAND_ICO%', urls.ico).replaceAll('%BRAND_APPLE%', urls.apple)
    },
    configureServer(server) {
      server.middlewares.use((req, res, next) => {
        const asset = entries.find(entry => entry.url === req.url?.split('?')[0])
        if (!asset) return next()
        res.setHeader('Content-Type', asset.name.endsWith('.svg') ? 'image/svg+xml' : asset.name.endsWith('.ico') ? 'image/x-icon' : 'image/png')
        res.setHeader('Cache-Control', 'no-cache')
        res.end(asset.bytes)
      })
    },
    generateBundle() {
      for (const entry of entries) this.emitFile({ type: 'asset', fileName: entry.name, source: entry.bytes })
    }
  }
  return { urls, plugin }
}
