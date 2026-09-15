import { defineConfig } from 'vitest/config'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'
import { fileURLToPath, URL } from 'node:url'
import { branding } from './build/branding.ts'

const brand = branding()

export default defineConfig({
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) }
  },
  plugins: [
    vue(),
    brand.plugin,
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: false,
      includeAssets: ['favicon.svg', 'favicon.ico', 'apple-touch-icon.png', 'pwa-192x192.png', 'pwa-512x512.png', 'pwa-maskable-512x512.png'],
      manifest: {
        name: 'Mashal Systems — Business Aid',
        short_name: 'Mashal',
        description: 'Customized software for business and life.',
        theme_color: '#0B1F3A',
        background_color: '#F7F9FC',
        display: 'standalone',
        start_url: '/',
        scope: '/',
        icons: [
          { src: brand.urls.pwa192, sizes: '192x192', type: 'image/png' },
          { src: brand.urls.pwa512, sizes: '512x512', type: 'image/png' },
          { src: brand.urls.maskable, sizes: '512x512', type: 'image/png', purpose: 'maskable' }
        ]
      },
      workbox: {
        navigateFallback: '/index.html',
        // Firebase serves its OAuth handler and helper iframe under reserved URLs.
        // Serving the cached app shell here breaks both redirect and popup completion.
        navigateFallbackDenylist: [/^\/__\//],
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff2}'],
        globIgnores: ['brand/**'],
        cleanupOutdatedCaches: true,
        clientsClaim: true,
        skipWaiting: true
      }
    })
  ],
  test: {
    environment: 'node',
    include: ['src/**/*.test.ts']
  }
})
