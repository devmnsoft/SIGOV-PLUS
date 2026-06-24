(function () {
  'use strict';

  const updateOnline = () => {
    const el = document.getElementById('sigov-online-indicator');
    if (!el) return;
    el.textContent = navigator.onLine ? 'online' : 'offline';
    el.className = navigator.onLine ? 'badge bg-success' : 'badge bg-warning text-dark';
  };

  window.addEventListener('online', updateOnline);
  window.addEventListener('offline', updateOnline);
  updateOnline();

  // O ciclo de vida do service worker fica centralizado em sigov-ui.js para evitar registro duplicado.

  window.sigovMobile = {
    clearSensitiveCache: function () {
      if (navigator.serviceWorker && navigator.serviceWorker.controller) {
        navigator.serviceWorker.controller.postMessage('SIGOV_CLEAR_SENSITIVE_CACHE');
      }
      localStorage.removeItem('sigov.mobile.pending');
      sessionStorage.clear();
    },
    setPendingSync: function (count) {
      localStorage.setItem('sigov.mobile.pending', String(count || 0));
      const el = document.getElementById('sigov-sync-pending');
      if (el) el.textContent = String(count || 0);
    }
  };

  const pending = Number(localStorage.getItem('sigov.mobile.pending') || '0');
  const el = document.getElementById('sigov-sync-pending');
  if (el) el.textContent = String(pending);
}());
