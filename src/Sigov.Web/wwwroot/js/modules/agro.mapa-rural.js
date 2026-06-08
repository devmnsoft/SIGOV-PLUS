(function ($) {
  'use strict';
  function row(values) { return '<tr>' + values.map(function (v) { return '<td>' + $('<div>').text(v == null ? '' : v).html() + '</td>'; }).join('') + '</tr>'; }
  function erro(xhr) { var map = { 401: 'Faça login para visualizar o Mapa Rural.', 403: 'Você não possui permissão para visualizar o Mapa Rural.', 500: 'Falha interna ao carregar o Mapa Rural.' }; $('#agroMapaErro').text(map[xhr.status] || 'Não foi possível carregar o Mapa Rural.').removeClass('d-none'); }
  $(function () {
    $('#agroMapaLoading').removeClass('d-none');
    $.getJSON('/api/agro/geo/camadas').done(function (r) { var items = (r.data && r.data.items) || []; $('#gridGeoCamadas tbody').html(items.length ? items.map(function (i) { return row([i.id, i.codigo, i.nome, i.tipoCamada, i.publica ? 'Sim' : 'Não', i.ativo ? 'Sim' : 'Não']); }).join('') : '<tr class="empty-state"><td colspan="6" class="text-center text-muted py-4">Nenhuma camada geográfica encontrada.</td></tr>'); }).fail(erro);
    $.getJSON('/api/agro/geo/feicoes').done(function (r) { var items = (r.data && r.data.items) || []; $('#gridGeoFeicoes tbody').html(items.length ? items.map(function (i) { return row([i.id, i.camadaId, i.nome, i.tipoGeometria, i.latitude, i.longitude, i.geoJson]); }).join('') : '<tr class="empty-state"><td colspan="7" class="text-center text-muted py-4">Nenhuma feição geográfica encontrada.</td></tr>'); }).fail(erro);
    $.ajax({ url: '/api/agro/geo/export.geojson', method: 'GET' }).done(function (data) { $('#geoJsonPreview').text(typeof data === 'string' ? data : JSON.stringify(data, null, 2)); }).fail(erro).always(function () { $('#agroMapaLoading').addClass('d-none'); });
  });
})(jQuery);
