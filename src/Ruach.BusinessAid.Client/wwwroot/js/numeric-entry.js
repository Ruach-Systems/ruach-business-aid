// Filter characters before Blazor receives an edit. Keep range and precision
// validation in NumericInput; never turn a pasted "1e3" into a different amount.
(() => {
  const snapshots = new WeakMap();
  const numeric = target => target instanceof HTMLInputElement && target.hasAttribute('data-numeric-kind');
  const allowed = (input, text) => (input.dataset.numericKind === 'money' ? /^[0-9]*(?:\.[0-9]*)?$/ : /^[0-9]*$/).test(text);
  const remember = input => snapshots.set(input, { value: input.value, start: input.selectionStart, end: input.selectionEnd });
  const insertedValue = (input, text) => input.value.slice(0, input.selectionStart) + text + input.value.slice(input.selectionEnd);
  document.addEventListener('focusin', event => {
    const input = event.target;
    if (!numeric(input)) return;
    if (input.dataset.numericKind === 'money' && /^[0-9]+\.00$/.test(input.value)) {
      const start = input.selectionStart, end = input.selectionEnd;
      input.value = input.value.slice(0, -3);
      input.setSelectionRange(Math.min(start, input.value.length), Math.min(end, input.value.length));
    }
    remember(input);
  }, true);
  document.addEventListener('beforeinput', event => {
    const input = event.target;
    if (!numeric(input)) return;
    remember(input);
    if (event.data !== null && !allowed(input, insertedValue(input, event.data))) event.preventDefault();
  }, true);
  document.addEventListener('paste', event => {
    const input = event.target;
    if (!numeric(input)) return;
    remember(input);
    if (event.clipboardData && !allowed(input, insertedValue(input, event.clipboardData.getData('text')))) event.preventDefault();
  }, true);
  // Covers autofill, drop, and input methods whose beforeinput cannot be cancelled.
  document.addEventListener('input', event => {
    const input = event.target;
    if (!numeric(input)) return;
    if (!allowed(input, input.value)) {
      const previous = snapshots.get(input);
      input.value = previous?.value ?? '';
      if (previous) input.setSelectionRange(previous.start, previous.end);
      event.stopImmediatePropagation();
      return;
    }
    remember(input);
  }, true);
})();
