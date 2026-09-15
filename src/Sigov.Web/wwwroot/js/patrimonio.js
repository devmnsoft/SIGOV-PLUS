(() => {
  'use strict';
  document.querySelectorAll('.needs-validation').forEach(form => form.addEventListener('submit', event => {
    if (!form.checkValidity()) { event.preventDefault(); event.stopPropagation(); }
    form.classList.add('was-validated');
  }));
  document.querySelectorAll('form[data-confirm]').forEach(form => form.addEventListener('submit', event => {
    if (!window.confirm(form.dataset.confirm)) { event.preventDefault(); return; }
    if (!form.checkValidity()) return;
    form.querySelectorAll('button[type="submit"], button:not([type])').forEach(button => {
      button.disabled = true;
      button.setAttribute('aria-busy', 'true');
    });
  }));
})();
