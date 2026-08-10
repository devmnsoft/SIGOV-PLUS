(function () {
  'use strict';
  const form = document.getElementById('loginForm');
  const button = document.getElementById('btnEntrar');
  const password = document.getElementById('Senha');
  const toggle = document.querySelector('[data-sigov-password-toggle]');

  toggle?.addEventListener('click', function () {
    if (!password) return;
    const show = password.type === 'password';
    password.type = show ? 'text' : 'password';
    toggle.setAttribute('aria-label', show ? 'Ocultar senha' : 'Mostrar senha');
    toggle.setAttribute('aria-pressed', String(show));
    toggle.querySelector('span').textContent = show ? 'Ocultar' : 'Mostrar';
    password.focus();
  });

  form?.addEventListener('submit', function (event) {
    if (!form.checkValidity()) {
      event.preventDefault();
      form.classList.add('was-validated');
      form.querySelector(':invalid')?.focus();
      return;
    }
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    button.querySelector('[data-button-label]').textContent = 'Validando acesso…';
  });
})();
