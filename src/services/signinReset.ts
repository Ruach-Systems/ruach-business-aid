const APP_STORAGE_PREFIXES = ['mashal:', 'mashal-business-aid']
const CACHE_PREFIXES = ['mashal', 'firebase']
const DATABASE_PREFIXES = ['mashal', 'firebase']

export async function clearSignInArtifacts(userId?: string): Promise<void> {
  const keysToClear = new Set<string>()
  for (let i = 0; i < localStorage.length; i++) {
    const key = localStorage.key(i)
    if (key && APP_STORAGE_PREFIXES.some((prefix) => key.startsWith(prefix))) {
      keysToClear.add(key)
    }
  }
  if (userId) keysToClear.add(`mashal:v1:${userId}`)
  keysToClear.forEach((key) => localStorage.removeItem(key))

  for (let i = sessionStorage.length - 1; i >= 0; i--) {
    const key = sessionStorage.key(i)
    if (key && APP_STORAGE_PREFIXES.some((prefix) => key.startsWith(prefix))) {
      sessionStorage.removeItem(key)
    }
  }

  if ('caches' in window) {
    const keys = await caches.keys()
    const removable = keys.filter((key) => CACHE_PREFIXES.some((prefix) => key.includes(prefix)))
    await Promise.all(removable.map((key) => caches.delete(key)))
  }

  if ('indexedDB' in window && typeof indexedDB.databases === 'function') {
    const databases = await indexedDB.databases()
    await Promise.all(
      databases.map(async ({ name }) => {
        if (!name) return
        const lower = name.toLowerCase()
        if (DATABASE_PREFIXES.some((prefix) => lower.includes(prefix))) {
          await indexedDB.deleteDatabase(name)
        }
      })
    )
  }
}
