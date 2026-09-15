import { describe, expect, it } from 'vitest'
import { authErrorMessage } from './authErrors'

describe('authErrorMessage', () => {
  it('gives a recoverable instruction when mobile pop-ups are blocked', () => {
    expect(authErrorMessage({ code: 'auth/popup-blocked' })).toContain('Allow pop-ups for MASHAL')
  })
  it('explains missing Firebase Authentication configuration', () => {
    expect(authErrorMessage({ code: 'auth/configuration-not-found' }))
      .toContain('enable Google')
  })

  it('does not expose an unknown Firebase error', () => {
    expect(authErrorMessage({ code: 'auth/internal-error', message: 'Sensitive SDK details' }))
      .toBe('Google sign-in could not be completed. Please try again.')
  })
})
