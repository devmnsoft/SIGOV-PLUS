(function ($) {
  'use strict';
  $(function () {
    $.getJSON('/api/educacao/dashboard').done(function (r) {
      const d = r.data || {};
      Object.keys(d).forEach(function (k) { $('[data-field="' + k + '"]').text(d[k]); });
      const alerts = d.alertas || [];
      $('#educacao-alertas').html(alerts.map(function (a) { return '<li>' + a + '</li>'; }).join(''));
    }).fail(function (xhr) {
      $('#educacao-alertas').html('<li>Não foi possível carregar o dashboard (' + xhr.status + ').</li>');
    });
  });
})(jQuery);
