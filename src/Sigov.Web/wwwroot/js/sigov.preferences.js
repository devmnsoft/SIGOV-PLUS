(() => {
  'use strict';
  const form = document.querySelector('[data-preferences-form]');
  if (!form) return;

  const apply = (key, value) => {
    const root = document.documentElement;
    if (key === 'theme') root.dataset.theme = value === 'auto' ? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light') : value;
    if (key === 'density') root.dataset.density = value;
    if (key === 'sidebar') root.dataset.sidebar = value;
  };
  form.querySelectorAll('[data-preview-preference]').forEach(select => {
    apply(select.dataset.previewPreference, select.value);
    select.addEventListener('change', () => apply(select.dataset.previewPreference, select.value));
  });

  form.addEventListener('submit', async event => {
    if (!form.checkValidity()) return;
    event.preventDefault();
    const button = form.querySelector('button[type="submit"]');
    button.disabled = true;
    button.setAttribute('aria-busy', 'true');
    button.textContent = 'Salvando…';
    try {
      const response = await fetch(form.action, { method: 'POST', body: new FormData(form), headers: { Accept: 'application/json', 'X-Requested-With': 'XMLHttpRequest' } });
      const result = await response.json().catch(() => ({}));
      if (!response.ok) throw new Error(result.message || 'Não foi possível salvar as preferências.');
      localStorage.setItem('sigov.preferences', JSON.stringify(Object.fromEntries(new FormData(form))));
      window.Sigov?.toast?.show?.(result.message || 'Preferências salvas.', 'success');
    } catch (error) {
      window.Sigov?.toast?.show?.(error.message, 'danger');
      if (!window.Sigov?.toast) alert(error.message);
    } finally {
      button.disabled = false;
      button.removeAttribute('aria-busy');
      button.textContent = button.dataset.submitLabel;
    }
  });
})();
