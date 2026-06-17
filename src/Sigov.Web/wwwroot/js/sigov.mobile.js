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

  if ('serviceWorker' in navigator) {
    var isLocal =
      location.hostname === 'localhost' ||
      location.hostname === '127.0.0.1' ||
      location.hostname === '[::1]';

    if (isLocal) {
      navigator.serviceWorker.getRegistrations()
        .then(function (registrations) {
          registrations.forEach(function (registration) {
            registration.unregister().catch(function () { });
          });
        })
        .catch(function () { });
    } else {
      window.addEventListener('load', function () {
        navigator.serviceWorker.register('/service-worker.js')
          .catch(function (error) {
            console.warn('Service Worker não registrado:', error);
          });
      });
    }
  }

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
