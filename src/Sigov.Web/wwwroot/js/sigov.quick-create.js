(() => {
  'use strict';
  const modal = document.getElementById('sigovQuickCreateModal');
  if (!modal) return;
  const backdrop = document.querySelector('[data-sigov-quick-create-close].sigov-modal-backdrop');
  let previousFocus;
  const close = () => { modal.hidden = true; if (backdrop) backdrop.hidden = true; previousFocus?.focus(); };
  const open = () => { previousFocus = document.activeElement; modal.hidden = false; if (backdrop) backdrop.hidden = false; modal.querySelector('a:not([aria-disabled="true"]),button')?.focus(); };
  document.querySelectorAll('[data-sigov-quick-create]').forEach(button => button.addEventListener('click', open));
  document.querySelectorAll('[data-sigov-quick-create-close]').forEach(button => button.addEventListener('click', close));
  modal.querySelectorAll('[aria-disabled="true"]').forEach(item => item.addEventListener('click', event => event.preventDefault()));
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && !modal.hidden) close(); });
})();
