(function (window, $) {
  'use strict';
  function firstAccess(key, title, message) {
    var storageKey = 'sigov-first-access-' + key;
    if (window.localStorage && localStorage.getItem(storageKey)) return;
    if (window.localStorage) localStorage.setItem(storageKey, '1');
    if (window.SigovHelp) window.SigovHelp.show(title || 'Primeiro acesso', '<p>' + (message || 'Siga as orientações da tela e consulte o manual quando necessário.') + '</p><p><strong>Dica:</strong> ações críticas pedem confirmação e operações com dados pessoais exibem cuidado LGPD.</p>');
    else if (window.SigovNotify) window.SigovNotify.info(message || 'Primeiro acesso orientado.', title || 'Primeiro acesso');
  }
  window.SigovOnboarding = { firstAccess: firstAccess };
  $(function(){
    $('.sigov-onboarding-task input[type="checkbox"]').on('change', function () { $(this).closest('.sigov-onboarding-task').toggleClass('text-decoration-line-through', this.checked); });
    var route = (location.pathname || '/').toLowerCase().replace(/\//g,'-').replace(/^-/, '') || 'dashboard';
    ['dashboard','minhacentral','seguranca-usuarios','seguranca-perfis','saas-modulos','saas-implantacao','auditoria-trilhas','manual'].forEach(function(k){ if(route === k) firstAccess(k, 'Orientação inicial', 'Esta tela possui guia, ajuda contextual, próximos passos e cuidados LGPD.'); });
  });
})(window, window.jQuery);
