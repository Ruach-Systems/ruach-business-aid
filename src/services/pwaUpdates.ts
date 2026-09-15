import { registerSW } from 'virtual:pwa-register'

const UPDATE_CHECK_INTERVAL_MS = 15 * 60 * 1000

export function setupPwaUpdates(): void {
  if (!('serviceWorker' in navigator)) return

  if (import.meta.env.DEV) {
    void navigator.serviceWorker.getRegistrations().then((registrations) =>
      Promise.all(registrations.map((registration) => registration.unregister()))
    )
    return
  }

  let reloading = false
  const hadController = Boolean(navigator.serviceWorker.controller)

  const reloadForUpdate = (): void => {
    if (reloading) return
    reloading = true
    window.location.reload()
  }

  // Workbox normally reloads after activation. This browser-level fallback also
  // covers installed mobile PWAs that replace the controller while backgrounded.
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (hadController) reloadForUpdate()
  })

  registerSW({
    immediate: true,
    onRegisteredSW: (_serviceWorkerUrl, registration) => {
      if (!registration) return

      const checkForUpdate = (): void => {
        if (navigator.onLine) void registration.update().catch(() => undefined)
      }

      window.addEventListener('online', checkForUpdate)
      window.addEventListener('focus', checkForUpdate)
      document.addEventListener('visibilitychange', () => {
        if (document.visibilityState === 'visible') checkForUpdate()
      })
      window.setInterval(checkForUpdate, UPDATE_CHECK_INTERVAL_MS)
      checkForUpdate()
    },
    onNeedReload: reloadForUpdate,
    onRegisterError: (error) => {
      console.warn('MASHAL could not register automatic app updates.', error)
    }
  })
}
