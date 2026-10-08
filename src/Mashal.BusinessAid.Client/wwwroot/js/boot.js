(async () => {
  const fetchWithRetry = async (uri, integrity) => {
    let lastError;
    for (let attempt = 1; attempt <= 4; attempt++) {
      try {
        const response = await fetch(uri, { cache: 'no-cache', credentials: 'same-origin', integrity });
        if (response.ok) return response;
        lastError = new Error(`HTTP ${response.status} while loading ${uri}`);
      } catch (error) { lastError = error; }
      await new Promise(resolve => setTimeout(resolve, attempt * 150));
    }
    throw lastError ?? new Error(`Unable to load ${uri}`);
  };
  const loadBootResource = (type, _, uri, integrity) =>
    type === 'dotnetjs' ? uri : fetchWithRetry(uri, integrity);

  try { await Blazor.start({ loadBootResource }); }
  catch (error) {
    console.error('Business Aid startup failed after retries.', error);
    const loading = document.querySelector('#app .app-loading');
    if (loading) loading.hidden = true;
    document.getElementById('blazor-error-ui')?.removeAttribute('hidden');
  }
})();
