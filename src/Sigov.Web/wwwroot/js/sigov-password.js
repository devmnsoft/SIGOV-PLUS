(() => {
    'use strict';
    const input = document.querySelector('[data-new-password]');
    if (!input) return;
    const form = input.closest('form');
    const rules = {
        length: value => value.length >= 12,
        upper: value => /[A-Z]/.test(value),
        lower: value => /[a-z]/.test(value),
        number: value => /[0-9]/.test(value),
        special: value => /[^A-Za-z0-9]/.test(value)
    };
    const update = () => Object.entries(rules).forEach(([name, valid]) => {
        const item = document.querySelector(`[data-rule="${name}"]`);
        if (item) item.classList.toggle('is-valid', valid(input.value));
    });
    input.addEventListener('input', update);
    update();

    document.querySelectorAll('input[type="password"]').forEach(passwordInput => {
        const toggle = document.createElement('button');
        toggle.type = 'button';
        toggle.className = 'btn btn-sm btn-outline-secondary mt-1';
        toggle.textContent = 'Mostrar senha';
        toggle.setAttribute('aria-pressed', 'false');
        toggle.addEventListener('click', () => {
            const show = passwordInput.type === 'password';
            passwordInput.type = show ? 'text' : 'password';
            toggle.textContent = show ? 'Ocultar senha' : 'Mostrar senha';
            toggle.setAttribute('aria-pressed', String(show));
        });
        passwordInput.insertAdjacentElement('afterend', toggle);
    });

    if (form) {
        const feedback = document.createElement('span');
        feedback.className = 'visually-hidden';
        feedback.setAttribute('aria-live', 'polite');
        form.appendChild(feedback);
        form.addEventListener('submit', () => {
            if (!form.checkValidity()) return;
            form.querySelectorAll('button[type="submit"]').forEach(button => {
                button.disabled = true;
                button.setAttribute('aria-disabled', 'true');
            });
            feedback.textContent = 'Processando a alteração de senha.';
        });
    }
})();
