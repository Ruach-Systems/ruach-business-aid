import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'

const sdk = vi.hoisted(() => ({
  popup: vi.fn(), redirect: vi.fn(), persistence: vi.fn(), result: vi.fn()
}))
vi.mock('firebase/app', () => ({ initializeApp: vi.fn(() => ({})) }))
vi.mock('firebase/auth', () => ({
  browserLocalPersistence: 'local',
  getAuth: () => ({}),
  setPersistence: sdk.persistence,
  getRedirectResult: sdk.result,
  signInWithPopup: sdk.popup,
  signInWithRedirect: sdk.redirect,
  GoogleAuthProvider: class { setCustomParameters() {} }
}))
vi.mock('firebase/firestore', () => ({
  initializeFirestore: () => ({}), persistentLocalCache: () => ({}), persistentMultipleTabManager: () => ({})
}))

describe('Google sign-in integration', () => {
  beforeEach(() => {
    vi.resetModules()
    vi.clearAllMocks()
    sdk.persistence.mockResolvedValue(undefined)
    sdk.redirect.mockResolvedValue(undefined)
    sdk.result.mockResolvedValue(null)
    vi.stubEnv('VITE_FIREBASE_API_KEY', 'test-key')
    vi.stubEnv('VITE_FIREBASE_PROJECT_ID', 'mashal-business-aid')
    vi.stubEnv('VITE_FIREBASE_APP_ID', 'test-app')
    vi.stubEnv('VITE_FIREBASE_AUTH_DOMAIN', 'mashal-business-aid.firebaseapp.com')
    vi.stubEnv('VITE_FIREBASE_MEASUREMENT_ID', '')
    vi.stubGlobal('window', {
      location: { hostname: 'mashal-business-aid.web.app' },
      matchMedia: () => ({ matches: false }), innerWidth: 393
    })
    vi.stubGlobal('navigator', { userAgent: 'Mozilla/5.0 (Linux; Android 14)' })
  })
  afterEach(() => { vi.unstubAllGlobals(); vi.unstubAllEnvs() })

  it('returns the signed-in mobile user without navigating away from web.app', async () => {
    sdk.popup.mockResolvedValue({ user: { uid: 'test-owner', displayName: 'Owner', email: 'owner@example.com' } })
    const { signInGoogle } = await import('./firebase')
    expect(await signInGoogle()).toMatchObject({ uid: 'test-owner', email: 'owner@example.com' })
    expect(sdk.popup).toHaveBeenCalledOnce()
    expect(sdk.redirect).not.toHaveBeenCalled()
  })

  it('surfaces popup blocking instead of silently returning through a broken redirect', async () => {
    const blocked = { code: 'auth/popup-blocked' }
    sdk.popup.mockRejectedValue(blocked)
    const { signInGoogle } = await import('./firebase')
    await expect(signInGoogle()).rejects.toBe(blocked)
    expect(sdk.redirect).not.toHaveBeenCalled()
  })

  it('keeps same-domain mobile redirects supported', async () => {
    window.location.hostname = 'mashal-business-aid.firebaseapp.com'
    const { signInGoogle } = await import('./firebase')
    expect(await signInGoogle()).toBeNull()
    expect(sdk.redirect).toHaveBeenCalledOnce()
    expect(sdk.popup).not.toHaveBeenCalled()
  })

  it('still consumes the Firebase redirect result on startup', async () => {
    const { completeGoogleRedirect } = await import('./firebase')
    await completeGoogleRedirect()
    expect(sdk.result).toHaveBeenCalledOnce()
  })
})
