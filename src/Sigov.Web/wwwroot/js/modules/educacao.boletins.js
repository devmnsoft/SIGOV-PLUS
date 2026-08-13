(function ($) {
  'use strict';
  function carregar() {
    const alunoId = Number($('#aluno-boletim').val());
    const $resultado = $('#boletim-resultado');
    if (!alunoId) { $resultado.html('<div class="alert alert-warning">Informe um identificador de aluno válido.</div>'); return; }
    $resultado.html('<div class="text-center py-5"><span class="spinner-border" aria-hidden="true"></span><span class="visually-hidden">Carregando</span></div>');
    $.getJSON('/api/educacao/boletins/' + alunoId)
      .done(function (r) {
        const itens = (r.data && r.data.itens) || [];
        if (!itens.length) { $resultado.html('<div class="text-center text-muted py-5"><i class="bi bi-inbox fs-1 d-block"></i>Nenhuma avaliação lançada para este aluno.</div>'); return; }
        $resultado.html('<table class="table table-hover align-middle"><thead><tr><th>Disciplina</th><th>Avaliação</th><th>Nota</th><th>Situação</th></tr></thead><tbody>' + itens.map(function (x) { return '<tr><td>' + x.componenteCurricular + '</td><td>' + x.avaliacao + '</td><td>' + (x.nota == null ? '—' : x.nota) + '</td><td><span class="badge text-bg-secondary">' + x.situacao + '</span></td></tr>'; }).join('') + '</tbody></table>');
      }).fail(function (xhr) { $resultado.html('<div class="alert alert-danger">' + (xhr.status === 403 ? 'Sem permissão para consultar dados acadêmicos.' : 'Não foi possível consultar o boletim.') + '</div>'); });
  }
  $(function () { $('#buscar-boletim, #consultar-boletim').on('click', carregar); });
})(jQuery);
