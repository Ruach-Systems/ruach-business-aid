export function resolveAppRoute(
  authReady: boolean,
  dataReady: boolean,
  hasUser: boolean,
  hasBusiness: boolean,
  path: string
): string | undefined {
  if (!authReady || !dataReady) return undefined
  if (!hasUser && path !== '/sign-in') return '/sign-in'
  if (hasUser && !hasBusiness && path !== '/onboarding') return '/onboarding'
  if (hasUser && hasBusiness && (path === '/sign-in' || path === '/onboarding' || path === '/')) return '/dashboard'
  return undefined
}
