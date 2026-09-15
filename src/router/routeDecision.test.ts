import { describe, expect, it } from 'vitest'
import { resolveAppRoute } from './routeDecision'

describe('app route decisions', () => {
  it('waits for account data before deciding whether onboarding is required', () => {
    expect(resolveAppRoute(true, false, true, false, '/sign-in')).toBeUndefined()
  })

  it('sends an existing hydrated account directly to the dashboard', () => {
    expect(resolveAppRoute(true, true, true, true, '/sign-in')).toBe('/dashboard')
  })

  it('uses onboarding only after hydration confirms no business exists', () => {
    expect(resolveAppRoute(true, true, true, false, '/sign-in')).toBe('/onboarding')
  })
})
