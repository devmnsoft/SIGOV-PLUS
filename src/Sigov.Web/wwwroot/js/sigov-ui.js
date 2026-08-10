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
  }

  document.addEventListener('click', handleAction);
  window.SigovUI = { init };
  document.addEventListener('DOMContentLoaded', () => {
    init(document);
  });
})();

(function(){
  'use strict';
  const store={get:(k,d)=>{try{return JSON.parse(localStorage.getItem(k))??d}catch{return d}},set:(k,v)=>{try{localStorage.setItem(k,JSON.stringify(v))}catch{}}};
  const sidebar=document.getElementById('sigovSidebar');
  if(sidebar){
    if(store.get('sigov.sidebar.compact',false)) sidebar.classList.add('is-compact');
    document.querySelectorAll('[data-sigov-sidebar-toggle]').forEach(b=>b.addEventListener('click',()=>{document.body.classList.add('sigov-sidebar-open');b.setAttribute('aria-expanded','true')}));
    document.querySelectorAll('[data-sigov-sidebar-close]').forEach(b=>b.addEventListener('click',()=>document.body.classList.remove('sigov-sidebar-open')));
    document.querySelectorAll('[data-sigov-sidebar-compact]').forEach(b=>b.addEventListener('click',()=>{sidebar.classList.toggle('is-compact');store.set('sigov.sidebar.compact',sidebar.classList.contains('is-compact'))}));
    const filter=document.querySelector('[data-sigov-menu-filter]'); if(filter) filter.addEventListener('input',()=>{const q=filter.value.toLowerCase();sidebar.querySelectorAll('.sigov-nav-link').forEach(a=>a.hidden=q&&!a.textContent.toLowerCase().includes(q));});
    const favs=store.get('sigov.menu.favorites',[]); sidebar.querySelectorAll('[data-favorite-key]').forEach(a=>{const key=a.dataset.favoriteKey,btn=a.querySelector('.sigov-favorite-toggle'); if(favs.includes(key)) btn?.classList.add('is-active'); btn?.addEventListener('click',e=>{e.preventDefault();e.stopPropagation();const list=store.get('sigov.menu.favorites',[]); const next=list.includes(key)?list.filter(x=>x!==key):[...list,key].slice(-12); store.set('sigov.menu.favorites',next); btn.classList.toggle('is-active'); window.dispatchEvent(new CustomEvent('sigov:favorites-changed',{detail:next}));});});
  }
  const quick=document.getElementById('sigovQuickCreateModal'); if(quick){let previous=null; const backdrop=document.querySelector('[data-sigov-quick-create-close].sigov-modal-backdrop'); const focusables=()=>Array.from(quick.querySelectorAll('a[href],button:not([disabled])')).filter(x=>!x.hidden&&x.getAttribute('aria-disabled')!=='true'); const open=()=>{previous=document.activeElement;quick.hidden=false;backdrop.hidden=false;document.body.classList.add('sigov-modal-open');focusables()[0]?.focus();document.dispatchEvent(new CustomEvent('sigov:quick-create-opened'));}; const close=()=>{quick.hidden=true;backdrop.hidden=true;document.body.classList.remove('sigov-modal-open');previous?.focus();}; document.querySelectorAll('[data-sigov-quick-create]').forEach(b=>b.addEventListener('click',open)); document.querySelectorAll('[data-sigov-quick-create-close]').forEach(b=>b.addEventListener('click',close)); quick.addEventListener('click',e=>{const item=e.target.closest('[data-sigov-quick-create-item]'); if(item) document.dispatchEvent(new CustomEvent('sigov:quick-create-selected',{detail:{title:item.dataset.title}}));}); document.addEventListener('keydown',e=>{if(quick.hidden)return; if(e.key==='Escape'){e.preventDefault();close();} if(e.key==='Tab'){const f=focusables(); if(!f.length)return; const first=f[0], last=f.at(-1); if(e.shiftKey&&document.activeElement===first){e.preventDefault();last.focus();}else if(!e.shiftKey&&document.activeElement===last){e.preventDefault();first.focus();}}}); }
  document.querySelectorAll('[data-sigov-theme-toggle]').forEach(b=>b.addEventListener('click',()=>{const cur=document.documentElement.dataset.sigovTheme||store.get('sigov.theme','light'); const next=cur==='dark'?'light':'dark'; document.documentElement.dataset.sigovTheme=next; store.set('sigov.theme',next);})); document.documentElement.dataset.sigovTheme=store.get('sigov.theme',document.documentElement.dataset.sigovTheme||'light');
  document.querySelectorAll('[data-sigov-user-menu]').forEach(b=>b.addEventListener('click',()=>{const m=document.querySelector('[data-sigov-user-dropdown]'); const open=m.hidden; m.hidden=!open; b.setAttribute('aria-expanded',String(open));}));
  document.querySelectorAll('[data-sigov-notification-toggle]').forEach(b=>b.addEventListener('click',()=>{const m=document.querySelector('[data-sigov-notification-menu]'); const open=m.hidden; m.hidden=!open; b.setAttribute('aria-expanded',String(open));}));
  const recent=store.get('sigov.recent.routes',[]).filter(x=>x.url!==location.pathname); recent.unshift({url:location.pathname,title:document.title.replace(' - SIGOV PLUS',''),at:new Date().toISOString()}); store.set('sigov.recent.routes',recent.slice(0,10));
})();
