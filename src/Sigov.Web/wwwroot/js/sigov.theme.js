(function (window, $) {
  'use strict';
  const primaryKey = 'sigov.theme';
  const legacyKey = 'sigov-theme';

  function getSavedTheme() {
    try {
      return localStorage.getItem(primaryKey) || localStorage.getItem(legacyKey) || 'light';
    } catch (e) {
      return 'light';
    }
  }

  function saveTheme(theme) {
    try {
      localStorage.setItem(primaryKey, theme);
      localStorage.setItem(legacyKey, theme);
    } catch (e) {
      /* localStorage indisponível ou em modo anônimo restritivo */
    }
  }

  function apply(theme) {
    const safeTheme = theme === 'dark' ? 'dark' : 'light';
    if (document.documentElement) {
      document.documentElement.setAttribute('data-sigov-theme', safeTheme);
      document.documentElement.dataset.sigovTheme = safeTheme;
    }
    if (document.body) {
      document.body.setAttribute('data-sigov-theme', safeTheme);
      document.body.dataset.sigovTheme = safeTheme;
    }
    document.querySelectorAll('[data-sigov-theme-toggle]').forEach(button => {
      button.setAttribute('aria-pressed', String(safeTheme === 'dark'));
    });
  }

  window.Sigov = window.Sigov || {};
  window.Sigov.theme = {
    get: getSavedTheme,
    apply: apply,
    toggle: function () {
      const current = getSavedTheme();
      const next = current === 'dark' ? 'light' : 'dark';
      saveTheme(next);
      apply(next);
      if (window.SigovNotify && typeof window.SigovNotify.info === 'function') {
        window.SigovNotify.info(`Tema ${next === 'dark' ? 'escuro' : 'claro'} aplicado.`, 'Tema');
      }
      return next;
    }
  };

  apply(getSavedTheme());

  function setupHandler() {
    document.removeEventListener('click', handleThemeClick);
    document.addEventListener('click', handleThemeClick);
  }

  function handleThemeClick(event) {
    const trigger = event.target.closest('[data-sigov-theme-toggle]');
    if (!trigger || trigger.disabled || trigger.getAttribute('aria-disabled') === 'true') return;
    event.preventDefault();
    event.stopPropagation();
    window.Sigov.theme.toggle();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', setupHandler);
  } else {
    setupHandler();
  }
})(window, window.jQuery);
