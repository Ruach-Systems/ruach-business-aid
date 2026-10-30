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
// Native required/date validation focuses its first invalid control before submit.
// Custom numeric drafts need the same behavior, scoped to the submitted form.
document.addEventListener('submit', event => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement) || !form.hasAttribute('data-numeric-validation')) return;
  const invalid = form.querySelector('input[aria-invalid="true"], select[aria-invalid="true"], textarea[aria-invalid="true"]');
  if (!invalid) return;
  event.preventDefault();
  event.stopImmediatePropagation();
  invalid.focus();
  invalid.scrollIntoView({ block: 'center', behavior: 'instant' });
}, true);

document.addEventListener('invalid', event => {
  const form = event.target.form;
  if (!form?.hasAttribute('data-numeric-validation')) return;
  const first = form.querySelector('input:invalid, select:invalid, textarea:invalid, [aria-invalid="true"]');
  if (first?.getAttribute('aria-invalid') !== 'true') return;
  event.preventDefault();
  first.focus();
  first.scrollIntoView({ block: 'center', behavior: 'instant' });
}, true);
