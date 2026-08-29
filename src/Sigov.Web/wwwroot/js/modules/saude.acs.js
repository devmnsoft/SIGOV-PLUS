(function () {
  function token() { return $('input[name="__RequestVerificationToken"]').val(); }
  function renderRows(table, items) {
    var tbody = table.find('tbody'); tbody.empty();
    if (!items || !items.length) { tbody.append('<tr><td colspan="3" class="text-muted">Nenhum registro encontrado.</td></tr>'); return; }
    items.forEach(function (x) { tbody.append('<tr><td>' + (x.id || '') + '</td><td>' + (x.nome || x.codigo || x.codigoPaciente || x.numero || x.tipoExame || 'Registro') + '</td><td><span class="badge bg-secondary">' + (x.status || x.situacao || (x.ativo ? 'ATIVO' : '')) + '</span></td></tr>'); });
  }
  function load() { var table = $('#saude-grid'); if (!table.length) return; $.getJSON(table.data('api'), { page: 1, pageSize: 20 }).done(function (r) { renderRows(table, r.data && (r.data.items || r.data.Items)); }).fail(function (xhr) { table.find('tbody').html('<tr><td colspan="3">Erro ' + xhr.status + '</td></tr>'); }); }
  function loadLookups() { $('.saude-lookup').each(function () { var select = $(this); $.getJSON(select.data('api'), { page: 1, pageSize: 100 }).done(function (r) { var items = r.data && (r.data.items || r.data.Items) || []; items.forEach(function (x) { select.append($('<option>', { value: x.id, text: x.nome || x.codigo || x.codigoPaciente || x.codigoDomicilio || x.codigoProfissional || ('Registro ' + x.id) })); }); }).fail(function () { select.after('<div class="text-danger small">Não foi possível carregar esta seleção no contexto atual.</div>'); }); }); }
  $(document).on('submit', '.saude-ajax-form', function (e) { e.preventDefault(); var form = $(this); var data = {}; form.serializeArray().forEach(function (i) { data[i.name] = i.value === '' ? null : i.value; }); $.ajax({ url: form.data('api'), method: 'POST', contentType: 'application/json', headers: { 'RequestVerificationToken': token() }, data: JSON.stringify(data) }).done(function () { $('.modal').modal('hide'); load(); }).fail(function (xhr) { alert((xhr.responseJSON && xhr.responseJSON.errors && xhr.responseJSON.errors[0]) || 'Falha na operação.'); }); });
  $(function () { load(); loadLookups(); });
}());
