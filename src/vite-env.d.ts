/// <reference types="vite/client" />
/// <reference types="vite-plugin-pwa/client" />

declare module 'virtual:mashal-brand' {
  const assets: Record<'logo' | 'reversedLogo' | 'icon' | 'reversedIcon' | 'favicon' | 'ico' | 'apple' | 'pwa192' | 'pwa512' | 'maskable', string>
  export default assets
}

interface ImportMetaEnv {
  readonly VITE_FIREBASE_API_KEY?: string
  readonly VITE_FIREBASE_AUTH_DOMAIN?: string
  readonly VITE_FIREBASE_PROJECT_ID?: string
  readonly VITE_FIREBASE_STORAGE_BUCKET?: string
  readonly VITE_FIREBASE_MESSAGING_SENDER_ID?: string
  readonly VITE_FIREBASE_APP_ID?: string
  readonly VITE_FIREBASE_MEASUREMENT_ID?: string
  readonly VITE_ENABLE_SIGNIN_RESET?: 'true' | 'false'
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
