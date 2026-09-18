(() => {
  'use strict';

  // Confirmação para forms
  document.querySelectorAll('form[data-confirm]').forEach(form => {
    form.addEventListener('submit', event => {
      if (!window.confirm(form.dataset.confirm)) {
        event.preventDefault();
        return;
      }
      if (!form.checkValidity()) return;
      form.querySelectorAll('button[type="submit"], button:not([type])').forEach(button => {
        button.disabled = true;
        button.setAttribute('aria-busy', 'true');
      });
    });
  });

  // Confirmação para botões dentro de forms
  document.querySelectorAll('button[data-confirm]').forEach(button => {
    button.addEventListener('click', event => {
      const msg = button.dataset.confirm;
      if (msg && !window.confirm(msg)) {
        event.preventDefault();
        event.stopPropagation();
      }
    });
  });

  // Validação padrão
  document.querySelectorAll('.needs-validation').forEach(form => {
    form.addEventListener('submit', event => {
      if (!form.checkValidity()) {
        event.preventDefault();
        event.stopPropagation();
      }
      form.classList.add('was-validated');
    });
  });
})();
