document.querySelectorAll('[data-prevent-double-submit]').forEach((form) => {
  form.addEventListener('submit', () => {
    const button = form.querySelector('button[type="submit"]');
    if (!button) return;
    button.disabled = true;
    button.setAttribute('aria-disabled', 'true');
  });
});
