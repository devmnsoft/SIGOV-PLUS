(function ($) {
  'use strict';
  function toast(message) { $('#integracoes-toast').removeClass('d-none').text(message); }
  function token() { return $('input[name="__RequestVerificationToken"]').val(); }
  $(function () {
    var path = window.location.pathname.toLowerCase();
    if (path.indexOf('/integracoes/dashboard') >= 0) {
      (window.sigovApi ? window.sigovApi.request('/api/integracoes/dashboard') : $.getJSON('/api/integracoes/dashboard')).then(function (r) {
        var d = r.data || {};
        $('[data-field="totalSistemas"]').text(d.totalSistemas || 0);
        $('[data-field="outboxPendentes"]').text(d.outboxPendentes || 0);
        $('[data-field="webhooksRecebidosHoje"]').text(d.webhooksRecebidosHoje || 0);
        $('[data-field="remessasPendentes"]').text(d.remessasPendentes || 0);
      }).catch(function (err) { toast(err && err.message ? err.message : 'Não foi possível carregar o dashboard.'); });
    }
    $('form').on('submit', function (e) { e.preventDefault(); toast('Operação estrutural pronta para Ajax seguro com antiforgery. Token: ' + (token() ? 'presente' : 'ausente')); });
  });
})(jQuery);
