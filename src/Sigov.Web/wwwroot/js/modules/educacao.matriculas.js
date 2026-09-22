(function ($) {
  'use strict';
  const endpoint = '/api/educacao/matriculas';
  const grid = '#grid-matriculas';
  const form = '#form-matricula';
  function toast(type, msg) { const el = $('#educacao-toast'); el.removeClass('d-none alert-success alert-danger alert-warning').addClass('alert-' + type).text(msg || 'Operação concluída.'); }
  function escapeHtml(value) { return $('<div>').text(value == null ? '' : String(value)).html(); }
  function syncContext() {
    const escola = $('#EscolaId').val();
    const ano = $('#AnoLetivoId').val();
    $('#AnoLetivoId option[data-escola]').each(function () {
      const pertence = !this.dataset.escola || !escola || this.dataset.escola === escola;
      $(this).prop('hidden', !pertence);
      if (!pertence && this.selected) $('#AnoLetivoId').val('');
    });
    $('#TurmaId option[data-escola]').each(function () {
      const pertence = (!escola || this.dataset.escola === escola) && (!ano || this.dataset.ano === ano);
      $(this).prop('hidden', !pertence);
      if (!pertence && this.selected) $('#TurmaId').val('');
    });
  }
  function load() {
    if (!$(grid).length) return;
    const filters = $('#filtros-matriculas').serialize();
    $(grid).html('<tr><td colspan="5" class="text-muted" role="status">Carregando matrículas...</td></tr>');
    $.getJSON(endpoint + (filters ? '?' + filters : '')).done(function (r) {
      const items = (r.data && r.data.items) || [];
      if (!items.length) { const filtered = $('#filtros-matriculas').serializeArray().some(x => x.value); $(grid).html('<tr><td colspan="5" class="text-muted">' + (filtered ? 'Nenhuma matrícula corresponde aos filtros informados.' : 'Ainda não há matrículas neste contexto.') + '</td></tr>'); return; }
      $(grid).html(items.map(function (x) { return '<tr><td>' + escapeHtml(x.numeroMatricula) + '</td><td>Código ' + escapeHtml(x.alunoId) + '</td><td>Código ' + escapeHtml(x.turmaId) + '</td><td><span class="badge bg-secondary">' + escapeHtml(x.status) + '</span></td><td class="text-end"><a class="btn btn-sm btn-outline-primary" href="/Educacao/MatriculaDetalhe/' + encodeURIComponent(x.id) + '">Ver detalhe</a></td></tr>'; }).join(''));
    }).fail(function (xhr) { $(grid).html('<tr><td colspan="5" class="text-danger">Não foi possível consultar as matrículas. Tente novamente.</td></tr>'); if (xhr.status === 401 || xhr.status === 403) toast('warning', 'Acesso indisponível para o contexto atual.'); else toast('danger', 'Erro ao consultar matrículas.'); });
  }
  function bind() {
    if (!form) return;
    $(form).on('submit', function (e) {
      e.preventDefault();
      if ($(this).valid && !$(this).valid()) return;
      const data = {};
      $(this).serializeArray().forEach(function (i) { if (i.name !== '__RequestVerificationToken') { const numeric = /(^|Id$|AnoLetivo$|Capacidade$|Valor|Peso|Pontuacao)/.test(i.name); data[i.name] = numeric && i.value !== '' ? Number(i.value) : i.value; } });
      const button = $(this).find('button[type="submit"]').prop('disabled', true).attr('aria-busy', 'true');
      $.ajax({ url: endpoint, method: 'POST', contentType: 'application/json', data: JSON.stringify(data), headers: { 'RequestVerificationToken': $(this).find('input[name="__RequestVerificationToken"]').val() } })
        .done(function (response) { const id = response && response.data; if (id) window.location.assign('/Educacao/MatriculaDetalhe/' + encodeURIComponent(id)); else { toast('danger', 'A gravação não retornou o registro persistido.'); button.prop('disabled', false).removeAttr('aria-busy'); } })
        .fail(function (xhr) { toast('danger', xhr.status === 403 ? 'Sem permissão para efetivar a matrícula.' : (xhr.responseJSON?.message || 'Falha ao salvar. Nenhuma vaga foi consumida.')); button.prop('disabled', false).removeAttr('aria-busy'); });
    });
    $('#filtros-matriculas').on('submit', function (e) { e.preventDefault(); load(); });
    $('#EscolaId, #AnoLetivoId').on('change', syncContext);
    syncContext();
  }
  function detailAction(path, payload, button) {
    button.prop('disabled', true).attr('aria-busy', 'true');
    return $.ajax({ url: endpoint + '/' + $('[data-matricula-id]').data('matricula-id') + '/' + path, method: 'POST', contentType: 'application/json', data: JSON.stringify(payload), headers: { 'RequestVerificationToken': $('[data-matricula-id] input[name="__RequestVerificationToken"]').val() } })
      .done(function () { toast('success', 'Operação confirmada. Atualizando o estado persistido...'); window.location.reload(); })
      .fail(function (xhr) { toast('danger', xhr.status === 404 ? 'Matrícula não encontrada no contexto autorizado.' : xhr.status === 403 ? 'Sem permissão para esta ação.' : (xhr.responseJSON?.message || 'A operação não foi concluída.')); button.prop('disabled', false).removeAttr('aria-busy'); });
  }
  function bindDetail() {
    if (!$('[data-matricula-id]').length) return;
    $('#confirmar-matricula').on('click', function () { detailAction('confirmar', {}, $(this)); });
    $('#transferir-matricula').on('submit', function (e) { e.preventDefault(); if (!window.confirm('Confirma a transferência? A matrícula atual será preservada como transferida.')) return; detailAction('transferir', { novaTurmaId: Number($('#nova-turma').val()), motivo: $('#motivo-transferencia').val() }, $(this).find('button[type="submit"]')); });
    $('#cancelar-matricula').on('submit', function (e) { e.preventDefault(); if (!window.confirm('Confirma o cancelamento desta matrícula? A ação preservará o histórico.')) return; detailAction('cancelar', { motivo: $('#motivo-cancelamento').val() }, $(this).find('button[type="submit"]')); });
  }
  $(function () { bind(); bindDetail(); load(); });
})(jQuery);
