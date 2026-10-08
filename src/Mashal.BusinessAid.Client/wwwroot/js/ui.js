window.mashalUi = {
  scrollAppToTop() {
    requestAnimationFrame(() => requestAnimationFrame(() => {
      const shell = document.querySelector('.app-shell');
      if (shell) {
        shell.scrollTop = 0;
        shell.scrollLeft = 0;
      }
    }));
  }
};

(() => {
  // Per-device display density, applied before Blazor renders to avoid a layout jump.
  const key = 'business-aid.compact-view';
  const root = document.documentElement;
  const apply = on => root.classList.toggle('density-compact', on);
  const read = () => { try { return localStorage.getItem(key) === '1'; } catch { return false; } };
  apply(read());
  window.addEventListener('storage', event => { if (event.key === key) apply(event.newValue === '1'); });
  window.mashalUi.isCompact = () => root.classList.contains('density-compact');
  window.mashalUi.setCompact = on => {
    apply(!!on);
    try { on ? localStorage.setItem(key, '1') : localStorage.removeItem(key); } catch { }
    return !!on;
  };
})();