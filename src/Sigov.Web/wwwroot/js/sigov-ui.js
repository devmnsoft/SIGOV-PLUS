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
  const icons = { success: 'success', error: 'error', warning: 'warning', info: 'info' };
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
    const icon = document.createElement('div');
    icon.className = 'sigov-toast__icon';
    icon.innerHTML = `<svg class="sigov-icon sigov-icon--20" width="20" height="20" aria-hidden="true"><use href="/icons/sigov-icons.svg#${icons[type] || icons.info}"></use></svg>`;
    const body = document.createElement('div');
    body.className = 'sigov-toast__body';
    const heading = document.createElement('strong');
    heading.textContent = title || defaults[type];
    const content = document.createElement('span');
    content.textContent = message;
    body.append(heading, content);
    const close = document.createElement('button');
    close.type = 'button'; close.setAttribute('aria-label', 'Fechar');
    close.innerHTML = '<svg class="sigov-icon sigov-icon--16" width="16" height="16" aria-hidden="true"><use href="/icons/sigov-icons.svg#close"></use></svg>';
    toast.append(icon, body, close);
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
  // API pública estável para views e módulos; SigovNotify permanece como alias legado.
  window.SigovToast = window.SigovNotify;
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
  window.SigovConfirm.open = window.SigovConfirm.show;

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
  window.SigovModal = { details: (title, html) => window.SigovHelp.show(title, html) };
  window.SigovLoading = { button: (button, isLoading, text) => {
    if (!button) return;
    if (isLoading) { button.dataset.originalText = button.innerHTML; button.disabled = true; button.innerHTML = `<span class="spinner-border spinner-border-sm me-2"></span>${text || 'Processando...'}`; }
    else { button.disabled = false; if (button.dataset.originalText) button.innerHTML = button.dataset.originalText; }
  } };
  window.SigovAlert = { inline: (container, message, variant) => {
    const target = typeof container === 'string' ? document.querySelector(container) : container;
    if (!target) return null;
    const alert = document.createElement('div');
    alert.className = `sigov-alert sigov-alert--${variant || 'info'}`;
    alert.setAttribute('role', variant === 'error' ? 'alert' : 'status');
    alert.textContent = message || '';
    target.replaceChildren(alert);
    return alert;
  } };
  window.SigovEmptyState = { render: (container, options) => {
    const target = typeof container === 'string' ? document.querySelector(container) : container;
    if (!target) return null;
    const opts = options || {}, empty = document.createElement('section');
    empty.className = 'sigov-empty-state'; empty.setAttribute('role', 'status');
    const title = document.createElement('h2'); title.textContent = opts.title || 'Nenhum resultado encontrado';
    const message = document.createElement('p'); message.textContent = opts.message || 'Ajuste os filtros ou cadastre um novo item.';
    empty.append(title, message); target.replaceChildren(empty); return empty;
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

  function setSidebar(open) {
    document.body.classList.toggle('sigov-sidebar-open', open);
    document.querySelectorAll('[data-sigov-sidebar-toggle]').forEach(button => button.setAttribute('aria-expanded', String(open)));
  }

  function handleAction(event) {
    const trigger = event.target.closest('[data-sigov-sidebar-toggle],[data-sigov-sidebar-close],[data-sigov-theme-toggle]');
    if (!trigger || trigger.disabled || trigger.getAttribute('aria-disabled') === 'true') return;
    if (trigger.matches('[data-sigov-sidebar-toggle]')) setSidebar(!document.body.classList.contains('sigov-sidebar-open'));
    if (trigger.matches('[data-sigov-sidebar-close]')) setSidebar(false);
    if (trigger.matches('[data-sigov-theme-toggle]')) {
      const next = document.documentElement.dataset.sigovTheme === 'dark' ? 'light' : 'dark';
      document.documentElement.dataset.sigovTheme = next;
      localStorage.setItem('sigov-theme', next);
      trigger.setAttribute('aria-pressed', String(next === 'dark'));
      SigovNotify.info(`Tema ${next === 'dark' ? 'escuro' : 'claro'} aplicado.`, 'Tema');
    }
  }

  function init(root) {
    const scope = root || document;
    const h = document.getElementById('sigov-toast-host');
    if (h && !h.dataset.sigovInitialized) {
      ['success','error','warning','info'].forEach(t => h.dataset[t] && showToast(t, h.dataset[t]));
      h.dataset.sigovInitialized = 'true';
    }
    const theme = localStorage.getItem('sigov-theme') || 'light'; document.documentElement.dataset.sigovTheme = theme;
    scope.querySelectorAll('[data-sigov-theme-toggle]').forEach(button => button.setAttribute('aria-pressed', String(theme === 'dark')));
    scope.querySelectorAll('img[data-sigov-image-fallback]').forEach(image => {
      if (image.dataset.sigovFallbackBound) return;
      image.dataset.sigovFallbackBound = 'true';
      image.addEventListener('error', () => {
        if (image.src.endsWith(image.dataset.sigovImageFallback)) return;
        image.src = image.dataset.sigovImageFallback;
      });
    });
    if (window.bootstrap) {
      scope.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(el => bootstrap.Tooltip.getOrCreateInstance(el));
      scope.querySelectorAll('[data-bs-toggle="popover"]').forEach(el => bootstrap.Popover.getOrCreateInstance(el));
    }
  }

  document.addEventListener('click', handleAction);
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && document.body.classList.contains('sigov-sidebar-open')) setSidebar(false);
  });
  window.SigovUI = { init };
  document.addEventListener('DOMContentLoaded', () => {
    init(document);
    const validationSummary = document.querySelector('[data-sigov-validation-summary]');
    if (validationSummary) validationSummary.focus();
  });
})();

(function () {
  'use strict';
  const store = {
    get: (key, fallback) => { try { return JSON.parse(localStorage.getItem(key)) ?? fallback; } catch { return fallback; } },
    set: (key, value) => { try { localStorage.setItem(key, JSON.stringify(value)); } catch { /* O armazenamento local é opcional. */ } },
    remove: key => { try { localStorage.removeItem(key); } catch { /* Sem estado para remover. */ } }
  };
  const body = document.body;
  const contextParts = [body.dataset.sigovUser, body.dataset.sigovTenant, body.dataset.sigovEntidade || '-', body.dataset.sigovExercicio || '-'];
  const hasOperationalContext = contextParts[0] && contextParts[1];
  const contextKey = hasOperationalContext ? contextParts.join(':') : null;
  const favoritesKey = contextKey ? `sigov.menu.favorites:${contextKey}` : null;
  const recentKey = contextKey ? `sigov.recent.routes:${contextKey}` : null;
  // Entradas sem identidade/contexto pertencem ao formato legado e nunca são atribuídas à sessão atual.
  store.remove('sigov.menu.favorites');
  store.remove('sigov.recent.routes');

  function safeInternalPath(value) {
    if (typeof value !== 'string' || !value.startsWith('/') || value.startsWith('//')) return null;
    try {
      const parsed = new URL(value, location.origin);
      if (parsed.origin !== location.origin || parsed.username || parsed.password) return null;
      return parsed.pathname + parsed.search;
    } catch { return null; }
  }
  function visibleRoutes() {
    return new Set(Array.from(document.querySelectorAll('a[href]')).map(a => safeInternalPath(a.getAttribute('href'))).filter(Boolean).map(x => x.split('?')[0]));
  }
  function getRecent() {
    if (!recentKey) return [];
    const allowed = visibleRoutes();
    const rows = store.get(recentKey, []).filter(row => row && safeInternalPath(row.url) && allowed.has(safeInternalPath(row.url).split('?')[0]));
    store.set(recentKey, rows);
    return rows;
  }
  window.SigovNavigationState = { getRecent, safeInternalPath };

  const sidebar = document.getElementById('sigovSidebar');
  if (sidebar) {
    if (store.get('sigov.sidebar.compact', false)) sidebar.classList.add('is-compact');
    document.querySelectorAll('[data-sigov-sidebar-toggle]').forEach(button => button.addEventListener('click', () => { document.body.classList.add('sigov-sidebar-open'); button.setAttribute('aria-expanded', 'true'); }));
    document.querySelectorAll('[data-sigov-sidebar-close]').forEach(button => button.addEventListener('click', () => document.body.classList.remove('sigov-sidebar-open')));
    document.querySelectorAll('[data-sigov-sidebar-compact]').forEach(button => button.addEventListener('click', () => { sidebar.classList.toggle('is-compact'); store.set('sigov.sidebar.compact', sidebar.classList.contains('is-compact')); }));
    const filter = document.querySelector('[data-sigov-menu-filter]');
    if (filter) filter.addEventListener('input', () => { const query = filter.value.toLowerCase(); sidebar.querySelectorAll('.sigov-nav-link').forEach(link => { link.hidden = Boolean(query) && !link.textContent.toLowerCase().includes(query); }); });
    const favorites = favoritesKey ? store.get(favoritesKey, []) : [];
    sidebar.querySelectorAll('[data-favorite-key]').forEach(row => {
      const key = row.dataset.favoriteKey, button = row.querySelector('.sigov-favorite-toggle');
      if (!button) return;
      const update = selected => { button.classList.toggle('is-active', selected); button.setAttribute('aria-pressed', String(selected)); };
      update(favorites.includes(key));
      button.addEventListener('click', event => {
        event.preventDefault(); event.stopPropagation();
        if (!favoritesKey) return;
        const list = store.get(favoritesKey, []);
        const next = list.includes(key) ? list.filter(item => item !== key) : [...list, key].slice(-12);
        store.set(favoritesKey, next); update(next.includes(key));
        window.dispatchEvent(new CustomEvent('sigov:favorites-changed', { detail: next }));
      });
    });
    const current = location.pathname.replace(/\/$/, '') || '/';
    sidebar.querySelectorAll('a.sigov-nav-link[href]').forEach(link => {
      const target = safeInternalPath(link.getAttribute('href'))?.split('?')[0].replace(/\/$/, '') || '';
      if (target === current) { link.setAttribute('aria-current', 'page'); link.closest('details')?.setAttribute('open', ''); }
      else link.removeAttribute('aria-current');
    });
  }

  document.querySelectorAll('[data-sigov-user-menu]').forEach(button => button.addEventListener('click', () => {
    const menu = document.querySelector('[data-sigov-user-dropdown]'); if (!menu) return;
    const open = menu.hidden; menu.hidden = !open; button.setAttribute('aria-expanded', String(open));
  }));
  document.querySelectorAll('[data-sigov-notification-toggle]').forEach(button => button.addEventListener('click', () => {
    const menu = document.querySelector('[data-sigov-notification-menu]'); if (!menu) return;
    const open = menu.hidden; menu.hidden = !open; button.setAttribute('aria-expanded', String(open));
  }));

  document.querySelectorAll('a[href*="/Auth/Logout"],form[action*="/Auth/Logout"] button').forEach(control => control.addEventListener('click', () => {
    if (favoritesKey) store.remove(favoritesKey); if (recentKey) store.remove(recentKey);
  }));

  const path = safeInternalPath(location.pathname);
  if (recentKey && path && !path.startsWith('/Auth/')) {
    const rows = getRecent().filter(row => row.url !== path);
    rows.unshift({ url: path, title: document.title.replace(' - SIGOV PLUS', ''), at: new Date().toISOString() });
    store.set(recentKey, rows.slice(0, 10));
  }
})();
