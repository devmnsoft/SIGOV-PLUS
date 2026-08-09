(() => {
    'use strict';
    const input = document.querySelector('[data-new-password]');
    if (!input) return;
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
})();
