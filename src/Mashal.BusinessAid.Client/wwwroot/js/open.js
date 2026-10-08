(() => {
  const home = 'https://businessaid.mashalsystems.com/';
  const android = navigator.userAgentData?.platform === 'Android' || /Android/i.test(navigator.userAgent);
  if (!android) {
    window.location.replace(home);
    return;
  }

  const fallback = 'https://businessaid.mashalsystems.com/open?fallback=1';
  const intent = 'intent://businessaid.mashalsystems.com/#Intent;scheme=https;package=com.android.chrome;S.browser_fallback_url=' + encodeURIComponent(fallback) + ';end';
  document.getElementById('chrome').href = intent;
  // A fallback navigation must not trigger another automatic launch loop.
  if (!new URLSearchParams(window.location.search).has('fallback')) {
    try { window.location.replace(intent); } catch { /* Keep the fallback actions visible. */ }
  }
})();
