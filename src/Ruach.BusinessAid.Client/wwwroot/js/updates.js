if ('serviceWorker' in navigator) {
  let reloading = false;
  const hadController = Boolean(navigator.serviceWorker.controller);
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (hadController && !reloading) { reloading = true; location.reload(); }
  });
  navigator.serviceWorker.register('/sw.js', { updateViaCache: 'none' }).then(registration => {
    const check = () => { if (navigator.onLine) registration.update().catch(() => {}); };
    check();
    addEventListener('online', check);
    addEventListener('focus', check);
    document.addEventListener('visibilitychange', () => { if (document.visibilityState === 'visible') check(); });
    setInterval(check, 15 * 60 * 1000);
  }).catch(() => {});
}
