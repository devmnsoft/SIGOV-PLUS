(function () {
  $(function () {
    $('#acs-sync-enviar').on('click', function () {
      var payload; try { payload = JSON.parse($('#acs-sync-payload').val()); } catch (e) { alert('JSON inválido.'); return; }
      $.ajax({ url: '/api/saude/acs/sync', method: 'POST', contentType: 'application/json', data: JSON.stringify(payload) })
        .done(function (r) { $('#acs-sync-result').text(JSON.stringify(r, null, 2)); })
        .fail(function (xhr) { $('#acs-sync-result').text('Erro ' + xhr.status + ': ' + xhr.responseText); });
    });
  });
}());
