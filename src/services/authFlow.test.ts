import { describe, expect, it } from 'vitest'
import { canUseGoogleRedirect, prefersGoogleRedirect, shouldRetryWithGoogleRedirect } from './authFlow'

describe('prefersGoogleRedirect', () => {
  it('uses redirect on phones', () => {
    expect(prefersGoogleRedirect({
      hostname: 'mashal-business-aid.firebaseapp.com',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)',
      standalone: false,
      viewportWidth: 390
    })).toBe(true)
  })

  it('uses redirect for an installed PWA', () => {
    expect(prefersGoogleRedirect({
      hostname: 'mashal-business-aid.firebaseapp.com',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent: 'Mozilla/5.0',
      standalone: true,
      viewportWidth: 1200
    })).toBe(true)
  })

  it('uses redirect in narrow embedded browsers', () => {
    expect(prefersGoogleRedirect({
      hostname: 'mashal-business-aid.firebaseapp.com',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent: 'Mozilla/5.0',
      standalone: false,
      viewportWidth: 490
    })).toBe(true)
  })

  it('keeps the popup on a regular desktop browser', () => {
    expect(prefersGoogleRedirect({
      hostname: 'mashal-business-aid.firebaseapp.com',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent: 'Mozilla/5.0',
      standalone: false,
      viewportWidth: 1280
    })).toBe(false)
  })

  it('uses popup during local development', () => {
    expect(prefersGoogleRedirect({
      hostname: '127.0.0.1',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)',
      standalone: false,
      viewportWidth: 390
    })).toBe(false)
  })

  it.each([
    ['Android phone', 'Mozilla/5.0 (Linux; Android 14)', false, 393],
    ['iPhone', 'Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X)', false, 390],
    ['installed PWA', 'Mozilla/5.0', true, 1200],
    ['narrow desktop', 'Mozilla/5.0', false, 490]
  ])('uses popup for %s when the helper is cross-origin', (_label, userAgent, standalone, viewportWidth) => {
    expect(prefersGoogleRedirect({
      hostname: 'mashal-business-aid.web.app',
      authDomain: 'mashal-business-aid.firebaseapp.com',
      userAgent, standalone, viewportWidth
    })).toBe(false)
  })

  it('supports same-domain redirect after an explicitly configured custom-domain setup', () => {
    expect(canUseGoogleRedirect({ hostname: 'business.example.com', authDomain: 'business.example.com' })).toBe(true)
  })

  it('never guesses a missing or differently configured auth domain', () => {
    expect(canUseGoogleRedirect({ hostname: 'business.example.com', authDomain: undefined })).toBe(false)
    expect(canUseGoogleRedirect({ hostname: 'business.example.com', authDomain: 'auth.example.com' })).toBe(false)
  })

  it('never retries a blocked popup through the broken cross-origin redirect', () => {
    expect(shouldRetryWithGoogleRedirect('auth/popup-blocked', {
      hostname: 'mashal-business-aid.web.app', authDomain: 'mashal-business-aid.firebaseapp.com'
    })).toBe(false)
  })

  it('can retry a blocked popup through a same-origin redirect', () => {
    expect(shouldRetryWithGoogleRedirect('auth/popup-blocked', {
      hostname: 'mashal-business-aid.firebaseapp.com', authDomain: 'mashal-business-aid.firebaseapp.com'
    })).toBe(true)
  })

  it.each(['auth/cancelled-popup-request', 'auth/popup-closed-by-user', 'auth/network-request-failed', undefined])('does not start a second redirect for %s', (code) => {
    expect(shouldRetryWithGoogleRedirect(code, {
      hostname: 'mashal-business-aid.firebaseapp.com', authDomain: 'mashal-business-aid.firebaseapp.com'
    })).toBe(false)
  })
})
