(function ($) {
  'use strict';
  function token() { return $('input[name="__RequestVerificationToken"]').first().val(); }
  function erro(xhr) { var map = { 401: 'Faça login para continuar.', 403: 'Você não possui permissão para esta operação.', 404: 'Registro não encontrado.', 422: 'Revise os campos informados.', 500: 'Falha interna. Tente novamente.' }; $('#agroGeoErro').text(map[xhr.status] || (xhr.responseJSON && xhr.responseJSON.message) || 'Operação não concluída.').removeClass('d-none'); }
  function payload($form) { var data = {}; $.each($form.serializeArray(), function (_, item) { data[item.name] = item.value; }); data.publica = $('#camadaPublica').is(':checked'); data.ativo = true; if (data.camadaId) { data.camadaId = Number(data.camadaId); } if (data.latitude) { data.latitude = Number(data.latitude); } else { data.latitude = null; } if (data.longitude) { data.longitude = Number(data.longitude); } else { data.longitude = null; } return data; }
  function row(values) { return '<tr>' + values.map(function (v) { return '<td>' + $('<div>').text(v == null ? '' : v).html() + '</td>'; }).join('') + '</tr>'; }
  function listar() {
    $('#agroGeoLoading').removeClass('d-none'); $('#agroGeoErro').addClass('d-none');
    var busca = $('#agroGeoBusca').val();
    $.getJSON('/api/agro/geo/camadas', { busca: busca }).done(function (r) { var items = (r.data && r.data.items) || []; $('#gridGeoCamadas tbody').html(items.length ? items.map(function (i) { return row([i.id, i.codigo, i.nome, i.tipoCamada, i.publica ? 'Sim' : 'Não', i.ativo ? 'Sim' : 'Não']); }).join('') : '<tr class="empty-state"><td colspan="6" class="text-center text-muted py-4">Nenhuma camada geográfica encontrada.</td></tr>'); }).fail(erro);
    $.getJSON('/api/agro/geo/feicoes', { busca: busca }).done(function (r) { var items = (r.data && r.data.items) || []; $('#gridGeoFeicoes tbody').html(items.length ? items.map(function (i) { return row([i.id, i.camadaId, i.nome, i.tipoGeometria, i.latitude, i.longitude, i.geoJson]); }).join('') : '<tr class="empty-state"><td colspan="7" class="text-center text-muted py-4">Nenhuma feição geográfica encontrada.</td></tr>'); }).fail(erro).always(function () { $('#agroGeoLoading').addClass('d-none'); });
  }
  $(function () {
    listar();
    $('#btnAgroGeoBuscar').on('click', listar);
    $('#formGeoCamada').on('submit', function (e) { e.preventDefault(); $.ajax({ url: '/api/agro/geo/camadas', method: 'POST', contentType: 'application/json', headers: { 'RequestVerificationToken': token() || '' }, data: JSON.stringify(payload($(this))) }).done(function () { $('#formGeoCamada')[0].reset(); listar(); }).fail(erro); });
    $('#formGeoFeicao').on('submit', function (e) { e.preventDefault(); $.ajax({ url: '/api/agro/geo/feicoes', method: 'POST', contentType: 'application/json', headers: { 'RequestVerificationToken': token() || '' }, data: JSON.stringify(payload($(this))) }).done(function () { $('#formGeoFeicao')[0].reset(); listar(); }).fail(erro); });
  });
})(jQuery);
