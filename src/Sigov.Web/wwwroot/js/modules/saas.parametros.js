(function ($) {
  'use strict';
  $.getJSON('/api/saas/parametros').done(function (response) {
    var rows = (response.data || []).map(function (item) {
      return '<tr><td><code>' + item.codigo + '</code></td><td>' + item.nome + '</td><td>' + item.escopo + '</td><td>' + item.tipoParametro + '</td><td>' + (item.sensivel ? 'Sim' : 'Não') + '</td></tr>';
    });
    $('#tenantParameters tbody').html(rows.join(''));
  });
  $('#paramResolver').on('click', function () {
    $.getJSON('/api/saas/parametros/' + $('#paramCodigo').val(), { tenantId: $('#paramTenantId').val(), moduloCodigo: $('#paramModulo').val() }).done(function (response) {
      $('#paramResolved').text(JSON.stringify(response.data, null, 2));
    });
  });
})(jQuery);
