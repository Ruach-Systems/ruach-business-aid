const authMessages: Record<string, string> = {
  'auth/popup-blocked': 'Allow pop-ups for MASHAL, then tap Continue with Google again. If you opened the app inside another app, open it directly in Chrome or Safari.',
  'auth/cancelled-popup-request': 'Another Google sign-in is already open. Finish that sign-in or close it and try again.',
  'auth/configuration-not-found': 'Google sign-in is not enabled for this Firebase project yet. In Firebase Console, open Authentication, choose Sign-in method, and enable Google.',
  'auth/operation-not-allowed': 'Google sign-in is not enabled for this Firebase project yet. In Firebase Console, open Authentication, choose Sign-in method, and enable Google.',
  'auth/unauthorized-domain': 'Google sign-in is not allowed from this website address. Add this domain to Firebase Authentication\'s authorized domains.',
  'auth/popup-closed-by-user': 'This browser closed the Google sign-in window. Open MASHAL in Chrome or Safari and try again.',
  'auth/network-request-failed': 'Google sign-in could not reach Firebase. Check your internet connection and try again.'
}

export function authErrorMessage(reason: unknown): string {
  const code = typeof reason === 'object' && reason !== null && 'code' in reason
    ? String((reason as { code?: unknown }).code || '')
    : ''

  return authMessages[code] || 'Google sign-in could not be completed. Please try again.'
}
