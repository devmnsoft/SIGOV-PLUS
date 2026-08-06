(() => {
    'use strict';
    const root = document.getElementById('design-system');
    if (!root) return;

    root.querySelector('[data-ds-toast]')?.addEventListener('click', () => {
        if (window.SigovToast?.success) window.SigovToast.success('Componente atualizado com sucesso.');
        else root.querySelector('[data-ds-toast]').textContent = 'Toast demonstrado com sucesso';
    });
    root.querySelector('[data-ds-theme]')?.addEventListener('click', () => {
        const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
        document.documentElement.dataset.theme = next;
        localStorage.setItem('sigov-theme', next);
    });
    root.querySelector('[data-ds-density]')?.addEventListener('click', () => {
        root.classList.toggle('sigov-density-compact');
    });
})();
