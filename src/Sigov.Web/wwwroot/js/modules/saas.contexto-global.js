(function ($) {
  'use strict';
  $('#ctxTrocar').on('click', function () {
    $.ajax({
      url: '/api/saas/contexto/trocar',
      method: 'POST',
      contentType: 'application/json',
      data: JSON.stringify({
        usuarioGlobalId: Number($('#ctxUsuarioGlobalId').val()),
        tenantDestinoId: Number($('#ctxTenantDestinoId').val()),
        entidadeDestinoId: $('#ctxEntidadeDestinoId').val() ? Number($('#ctxEntidadeDestinoId').val()) : null,
        motivo: $('#ctxMotivo').val()
      })
    }).done(function () { $('#ctxLogs').trigger('click'); });
  });
  $('#ctxLogs').on('click', function () {
    $.getJSON('/api/saas/contexto/logs', { usuarioGlobalId: $('#ctxUsuarioGlobalId').val() }).done(function (response) {
      $('#ctxLogsOutput').text(JSON.stringify(response.data, null, 2));
    });
  });
})(jQuery);
