(function () {
  'use strict';

  function isExternalAsyncListenerMessage(message) {
    return message && message.indexOf('A listener indicated an asynchronous response by returning true') >= 0;
  }

  window.addEventListener('unhandledrejection', function (event) {
    var message = event && event.reason && event.reason.message
      ? event.reason.message
      : String(event && event.reason || '');

    if (isExternalAsyncListenerMessage(message)) {
      console.warn('Aviso externo do navegador/extensão ignorado:', message);
      event.preventDefault();
      return;
    }

    console.error('Promise não tratada:', event.reason);
  });

  window.addEventListener('error', function (event) {
    if (!event || !event.message) return;

    if (isExternalAsyncListenerMessage(event.message)) {
      console.warn('Aviso externo do navegador/extensão ignorado:', event.message);
      event.preventDefault();
    }
  });
  const icons = { success: '✓', error: '!', warning: '⚠', info: 'i' };
  const defaults = { success: 'Sucesso', error: 'Erro', warning: 'Atenção', info: 'Informação' };
  function host() {
    let el = document.getElementById('sigov-toast-host') || document.getElementById('sigov-toast-container');
    if (!el) { el = document.createElement('div'); el.id = 'sigov-toast-host'; el.className = 'sigov-toast-host'; document.body.appendChild(el); }
    return el;
  }
  function showToast(type, message, title) {
    if (!message) return;
    const toast = document.createElement('div');
    toast.className = `sigov-toast sigov-toast--${type}`;
    toast.innerHTML = `<div class="sigov-toast__icon">${icons[type] || icons.info}</div><div class="sigov-toast__body"><strong>${title || defaults[type]}</strong><span>${message}</span></div><button type="button" aria-label="Fechar">×</button>`;
    toast.querySelector('button').addEventListener('click', () => toast.remove());
    host().appendChild(toast);
    setTimeout(() => toast.remove(), 5200);
  }
  window.SigovNotify = {
    success: (message, title) => showToast('success', message, title),
    error: (message, title) => showToast('error', message, title),
    warning: (message, title) => showToast('warning', message, title),
    info: (message, title) => showToast('info', message, title)
  };
  window.SigovConfirm = { show: (options) => new Promise((resolve) => {
    const opts = options || {}, modalEl = document.getElementById('sigovConfirmModal');
    if (!modalEl || !window.bootstrap) { resolve(false); return; }
    modalEl.querySelector('#sigovConfirmTitle').textContent = opts.title || 'Confirmar operação';
    modalEl.querySelector('#sigovConfirmMessage').textContent = opts.message || 'Confirme antes de continuar. Esta operação será auditada.';
    const ok = modalEl.querySelector('[data-sigov-confirm-ok]');
    ok.textContent = opts.confirmText || 'Confirmar';
    ok.className = `sigov-btn ${opts.variant === 'danger' ? 'sigov-btn--danger' : 'sigov-btn--primary'}`;
    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const done = (value) => { modalEl.removeEventListener('hidden.bs.modal', onHidden); ok.removeEventListener('click', onOk); resolve(value); };
    const onOk = () => { modal.hide(); done(true); };
    const onHidden = () => done(false);
    ok.addEventListener('click', onOk, { once: true }); modalEl.addEventListener('hidden.bs.modal', onHidden, { once: true }); modal.show();
  }) };

  window.SigovHelp = { show: (title, html) => {
    let el = document.getElementById('sigovHelpModal');
    if (!el) {
      el = document.createElement('div'); el.className = 'modal fade sigov-modal'; el.id = 'sigovHelpModal'; el.tabIndex = -1;
      el.innerHTML = '<div class="modal-dialog modal-lg modal-dialog-centered"><div class="modal-content"><div class="modal-header"><h2 class="modal-title h5" id="sigovHelpTitle"></h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Fechar"></button></div><div class="modal-body" id="sigovHelpBody"></div><div class="modal-footer"><button class="sigov-btn sigov-btn--primary" data-bs-dismiss="modal">Entendi</button></div></div></div>';
      document.body.appendChild(el);
    }
    el.querySelector('#sigovHelpTitle').textContent = title || 'Ajuda rápida';
    el.querySelector('#sigovHelpBody').innerHTML = html || '<p>Use esta tela seguindo as orientações exibidas.</p>';
    if (window.bootstrap) bootstrap.Modal.getOrCreateInstance(el).show();
  } };
  window.SigovLoading = { button: (button, isLoading, text) => {
    if (!button) return;
    if (isLoading) { button.dataset.originalText = button.innerHTML; button.disabled = true; button.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${text || 'Processando...'}`; }
    else { button.disabled = false; if (button.dataset.originalText) button.innerHTML = button.dataset.originalText; }
  } };

  function shouldUseServiceWorker() {
    var host = window.location.hostname;
    return window.isSecureContext && host !== 'localhost' && host !== '127.0.0.1' && host !== '[::1]';
  }

  if ('serviceWorker' in navigator) {
    window.addEventListener('load', function () {
      if (!shouldUseServiceWorker()) {
        navigator.serviceWorker.getRegistrations()
          .then(function (registrations) { registrations.forEach(function (registration) { registration.unregister(); }); })
          .catch(function (error) { console.warn('Não foi possível limpar service workers locais do SIGOV:', error); });
        return;
      }

      navigator.serviceWorker.register('/service-worker.js')
        .catch(function (error) { console.warn('Service worker SIGOV não registrado:', error); });
    });
  }

  document.addEventListener('DOMContentLoaded', () => {
    const h = document.getElementById('sigov-toast-host');
    if (h) ['success','error','warning','info'].forEach(t => h.dataset[t] && showToast(t, h.dataset[t]));
    document.querySelectorAll('[data-sigov-sidebar-toggle]').forEach(btn => btn.addEventListener('click', () => document.body.classList.toggle('sigov-sidebar-open')));
    document.querySelectorAll('[data-sigov-sidebar-close]').forEach(el => el.addEventListener('click', () => document.body.classList.remove('sigov-sidebar-open')));
    const theme = localStorage.getItem('sigov-theme') || 'light'; document.documentElement.dataset.sigovTheme = theme;
    document.querySelectorAll('[data-sigov-theme-toggle]').forEach(btn => btn.addEventListener('click', () => { const next = document.documentElement.dataset.sigovTheme === 'dark' ? 'light' : 'dark'; document.documentElement.dataset.sigovTheme = next; localStorage.setItem('sigov-theme', next); SigovNotify.info(`Tema ${next === 'dark' ? 'escuro' : 'claro'} aplicado.`, 'Tema'); }));
  });
})();
