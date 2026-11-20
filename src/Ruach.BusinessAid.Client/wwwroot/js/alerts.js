// Locally bundled CoolAlertJS 1.0.4. Escape all user-controlled content before its HTML templates.
CoolAlert.initializeStyles({ background: '#FFFFFF', text: '#292530', title: '#141821', primary: '#B21F32', secondary: '#EFE8F1', success: '#2E6B57', error: '#B21F32', warning: '#8A4B08', info: '#321C3B', question: '#321C3B', closeBtn: '#696270' });
const alertText = value => { const node = document.createElement('span'); node.textContent = value; return node.innerHTML; };
let modalQueue = Promise.resolve();
function modal(options) {
  const task = modalQueue.then(async () => {
    const previous = document.activeElement;
    const app = document.getElementById('app');
    const wasInert = app?.inert;
    const result = CoolAlert.show(options);
    const dialog = [...document.querySelectorAll('.cool-alert-modal')].at(-1);
    if (app) app.inert = true;
    dialog?.setAttribute('role', 'dialog');
    dialog?.setAttribute('aria-modal', 'true');
    dialog?.setAttribute('aria-label', options.title);
    const buttons = () => [...dialog.querySelectorAll('button:not([disabled])')];
    const cancel = dialog?.querySelector('[data-action="cancel"]');
    // Focus can fall back to body without a focusin event on another element.
    // Check after the browser completes the transition, including Tab navigation.
    let focusFrame;
    function scheduleFocusCheck() {
      cancelAnimationFrame(focusFrame);
      focusFrame = requestAnimationFrame(() => {
        if (!dialog.isConnected || dialog.contains(document.activeElement)) return;
        (cancel || buttons()[0])?.focus({ preventScroll: true });
        if (!dialog.contains(document.activeElement)) scheduleFocusCheck();
      });
    }
    scheduleFocusCheck();
    const keydown = event => {
      if (event.key === 'Escape') { event.preventDefault(); event.stopImmediatePropagation(); (cancel || dialog?.querySelector('[data-action="confirm"]'))?.click(); }
      if (event.key === 'Tab') {
        const list = buttons(); const index = list.indexOf(document.activeElement);
        if (event.shiftKey && index <= 0) { event.preventDefault(); list.at(-1)?.focus(); }
        else if (!event.shiftKey && index === list.length - 1) { event.preventDefault(); list[0]?.focus(); }
      }
    };
    const keepFocus = event => { if (!dialog.contains(event.target)) scheduleFocusCheck(); };
    dialog.addEventListener('focusout', scheduleFocusCheck);
    document.addEventListener('focusin', keepFocus, true);
    document.addEventListener('keydown', keydown, true);
    try { return await result; }
    finally {
      cancelAnimationFrame(focusFrame);
      dialog.removeEventListener('focusout', scheduleFocusCheck);
      document.removeEventListener('focusin', keepFocus, true);
      document.removeEventListener('keydown', keydown, true);
      dialog.removeAttribute('role');
      dialog.removeAttribute('aria-modal');
      if (app) app.inert = wasInert;
      if (previous?.isConnected) previous.focus();
    }
  });
  modalQueue = task.catch(() => {});
  return task;
}
// Horizontal touch/pen gestures dismiss only the notification being swiped.
function enableToastSwipe(toast) {
  if (!toast) return;
  let gesture;
  const reset = () => { gesture = null; toast.style.translate = ''; toast.style.opacity = ''; };
  toast.addEventListener('pointerdown', event => {
    if (gesture || !event.isPrimary || event.pointerType === 'mouse' || event.target.closest('button')) return;
    gesture = { id: event.pointerId, x: event.clientX, y: event.clientY, dx: 0 };
    toast.setPointerCapture(event.pointerId);
  });
  toast.addEventListener('pointermove', event => {
    if (!gesture || gesture.id !== event.pointerId) return;
    const dx = event.clientX - gesture.x, dy = event.clientY - gesture.y;
    if (Math.abs(dy) > Math.max(12, Math.abs(dx))) { reset(); return; }
    gesture.dx = dx;
    if (Math.abs(dx) < 8) return;
    toast.style.translate = `${dx}px 0`;
    toast.style.opacity = String(Math.max(.3, 1 - Math.abs(dx) / toast.offsetWidth));
  });
  toast.addEventListener('pointerup', event => {
    if (!gesture || gesture.id !== event.pointerId) return;
    const dismiss = Math.abs(gesture.dx) >= Math.max(56, toast.offsetWidth * .25);
    reset();
    if (toast.hasPointerCapture(event.pointerId)) toast.releasePointerCapture(event.pointerId);
    if (dismiss) toast.querySelector('.cool-alert-toast-close')?.click();
  });
  toast.addEventListener('pointercancel', reset);
  toast.addEventListener('lostpointercapture', reset);
}
window.businessAlerts = {
  async confirm(text, confirmText = 'Confirm', cancelText = 'Cancel') {
    const result = await modal({ title: 'Please confirm', text: alertText(text), icon: 'warning', showCancelButton: true, confirmButtonText: alertText(confirmText), cancelButtonText: alertText(cancelText), allowOutsideClick: false });
    return result.isConfirmed === true;
  },
  success(text) {
    CoolAlert.show({ toast: true, position: 'top-right', icon: 'success', title: alertText(text), text: ' ', duration: 5000 });
    const toast = [...document.querySelectorAll('.cool-alert-toast')].at(-1);
    enableToastSwipe(toast);
    toast?.setAttribute('role', 'status');
    toast?.querySelector('.cool-alert-toast-close')?.setAttribute('aria-label', 'Dismiss notification');
  },
  error(text) { return modal({ title: 'Something went wrong', text: alertText(text), icon: 'error', showCancelButton: false, confirmButtonText: 'OK', allowOutsideClick: false }); },
  warning(text) { return modal({ title: 'Please review', text: alertText(text), icon: 'warning', showCancelButton: false, confirmButtonText: 'OK' }); }
};
