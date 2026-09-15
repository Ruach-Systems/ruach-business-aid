export interface AuthClientEnvironment {
  hostname: string
  authDomain: string | undefined
  userAgent: string
  standalone: boolean
  viewportWidth: number
}

export function canUseGoogleRedirect(environment: Pick<AuthClientEnvironment, 'hostname' | 'authDomain'>): boolean {
  const localDevelopment = ['127.0.0.1', 'localhost', '[::1]'].includes(environment.hostname)
  // A cross-origin redirect helper loses its result when third-party storage is partitioned.
  return !localDevelopment && environment.hostname === environment.authDomain
}

export function prefersGoogleRedirect(environment: AuthClientEnvironment): boolean {
  if (!canUseGoogleRedirect(environment)) return false

  const mobileDevice = /Android|iPhone|iPad|iPod/i.test(environment.userAgent)
  return mobileDevice || environment.standalone || environment.viewportWidth < 768
}

export function shouldRetryWithGoogleRedirect(code: string | undefined, environment: Pick<AuthClientEnvironment, 'hostname' | 'authDomain'>): boolean {
  return code === 'auth/popup-blocked' && canUseGoogleRedirect(environment)
}
