(() => {
  const home = new URL('/', window.location.href).href;
  const android = navigator.userAgentData?.platform === 'Android' || /Android/i.test(navigator.userAgent);
  if (!android) {
    window.location.replace(home);
    return;
  }

  const fallback = new URL('/open?fallback=1', home).href;
  const intent = 'intent://' + window.location.host + '/#Intent;scheme=' + window.location.protocol.slice(0, -1) + ';package=com.android.chrome;S.browser_fallback_url=' + encodeURIComponent(fallback) + ';end';
  document.getElementById('chrome').href = intent;
  // A fallback navigation must not trigger another automatic launch loop.
  if (!new URLSearchParams(window.location.search).has('fallback')) {
    try { window.location.replace(intent); } catch { /* Keep the fallback actions visible. */ }
  }
})();
