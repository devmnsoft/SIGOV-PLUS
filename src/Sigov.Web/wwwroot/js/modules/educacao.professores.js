(function ($) {
  'use strict';
  const endpoint = '/api/educacao/professores';
  const grid = '#grid-professores';
  const form = '#form-professor';
  function toast(type, msg) { const el = $('#educacao-toast'); el.removeClass('d-none alert-success alert-danger alert-warning').addClass('alert-' + type).text(msg || 'Operação concluída.'); }
  function mask(v) { return v ? String(v).replace(/.(?=.{4})/g, '*') : ''; }
  function load() {
    if (!grid) return;
    $(grid).html('<tr><td colspan="6">Carregando...</td></tr>');
    $.getJSON(endpoint).done(function (r) {
      const items = (r.data && r.data.items) || [];
      if (!items.length) { $(grid).html('<tr><td colspan="6" class="text-muted">Nenhum registro encontrado.</td></tr>'); return; }
      $(grid).html(items.map(function (x) { return '<tr><td>' + (x.codigo || x.codigoAluno || x.numeroMatricula || x.titulo || x.protocolo || x.id) + '</td><td>' + (x.nome || x.pessoaId || x.alunoId || x.turmaId || '') + '</td><td><span class="badge bg-secondary">' + (x.status || x.situacao || 'ATIVO') + '</span></td><td>' + mask(x.nis || x.cartaoSus || '') + '</td></tr>'; }).join(''));
    }).fail(function (xhr) { $(grid).html('<tr><td colspan="6">Falha ao carregar.</td></tr>'); if (xhr.status === 401 || xhr.status === 403) toast('warning', 'Acesso não autorizado.'); });
  }
  function bind() {
    if (!form) return;
    $(form).on('submit', function (e) {
      e.preventDefault();
      if ($(this).valid && !$(this).valid()) return;
      const data = {};
      $(this).serializeArray().forEach(function (i) { if (i.name !== '__RequestVerificationToken') { const numeric = /(^|Id$|AnoLetivo$|Capacidade$|Valor|Peso|Pontuacao)/.test(i.name); data[i.name] = numeric && i.value !== '' ? Number(i.value) : i.value; } });
      $.ajax({ url: endpoint, method: 'POST', contentType: 'application/json', data: JSON.stringify(data), headers: { 'RequestVerificationToken': $(this).find('input[name="__RequestVerificationToken"]').val() } })
        .done(function () { toast('success', 'Registro salvo com sucesso.'); load(); })
        .fail(function (xhr) { toast('danger', xhr.status === 403 ? 'Sem permissão.' : 'Falha ao salvar.'); });
    });
  }
  $(function () { bind(); load(); });
})(jQuery);
