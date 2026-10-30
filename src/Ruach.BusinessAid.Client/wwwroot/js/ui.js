window.mashalUi = {
  clearFieldErrors(form) {
    for (const input of form.querySelectorAll('input,select,textarea')) {
      const error = errors.get(input);
      if (!error) continue;
      input.setAttribute('aria-describedby', (input.getAttribute('aria-describedby') || '').split(' ').filter(id => id !== error.id).join(' '));
      input.removeAttribute('aria-invalid'); error.remove(); errors.delete(input);
    }
  },
  revealExpenseSuccess(notice) {
    notice.focus({ preventScroll: true });
    notice.scrollIntoView({ block: 'center', behavior: 'instant' });
  },
  revealExpenseEditor(form) {
    form.querySelector('#expense-description')?.focus({ preventScroll: true });
    form.scrollIntoView({ block: 'start', behavior: 'instant' });
  },
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
const revealNumericErrors = form => form.querySelectorAll("[data-numeric-invalid]").forEach(input =>
  input.dispatchEvent(new CustomEvent("validationsubmit", { bubbles: true })));
const errors = new WeakMap();
function validateField(input, show) {
  if (input.disabled || input.hasAttribute('data-numeric-invalid')) return false;
  const label = input.labels?.[0]?.textContent.trim().toLowerCase() || 'a value';
  let message = '';
  if (input.required && (!input.value.trim() || input.value === '00000000-0000-0000-0000-000000000000')) message = `${input.tagName === 'SELECT' ? 'Select' : 'Enter'} ${label}.`;
  else if (input.type === 'date' && input.value && input.value < '2026-01-01') message = 'Enter a valid date within the allowed range.';
  else if (!input.validity.valid) message = input.type === 'date' ? 'Enter a valid date within the allowed range.' : `Enter a valid ${label}.`;
  let error = errors.get(input);
  if (show && message && !error) {
    error = document.createElement('small'); error.className = 'numeric-error custom-field-error';
    error.id = `field-error-${crypto.randomUUID()}`; error.setAttribute('role', 'alert');
    input.insertAdjacentElement('afterend', error); errors.set(input, error);
    input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), error.id].filter(Boolean).join(' '));
  }
  if (error) { error.textContent = message; error.hidden = !message; input.setAttribute('aria-invalid', message ? 'true' : 'false'); }
  return !!message;
}
document.addEventListener('submit', event => {
  const form = event.target;
  if (!(form instanceof HTMLFormElement)) return;
  revealNumericErrors(form);
  let first;
  for (const input of form.querySelectorAll('input,select,textarea')) {
    const invalid = input.dataset.numericInvalid === 'true' || validateField(input, true);
    if (invalid && !first) first = input;
  }
  if (!first) return;
  event.preventDefault(); event.stopImmediatePropagation(); first.focus();
  first.scrollIntoView({ block: 'center', behavior: 'instant' });
}, true);
for (const name of ['input','change']) document.addEventListener(name, event => {
  const input = event.target;
  if (!(input instanceof HTMLInputElement || input instanceof HTMLSelectElement || input instanceof HTMLTextAreaElement)) return;
  const invalid = validateField(input, errors.has(input) || (input.type === 'date' && !input.form));
  if (input.type === 'date' && !input.form && invalid) event.stopImmediatePropagation();
}, true);