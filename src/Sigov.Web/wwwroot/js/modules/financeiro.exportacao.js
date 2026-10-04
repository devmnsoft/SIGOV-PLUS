// SIGOV PLUS · Financeiro SIAFIC — download de relatórios CSV/JSON via endpoint de exportação.
// Botões esperados: class="btn-export-csv|btn-export-json" com data-resource
// (empenhos, liquidacoes, pagamentos, receitas, orcamento-despesas, orcamento-receitas).
(function () {
  const F = window.sigovFin;
  if (!F) return;

  async function baixar(btn, formato) {
    const recurso = $(btn).data('resource');
    if (!recurso) return;
    btn.disabled = true;
    try {
      await F.baixarArquivo(String(recurso), formato);
      F.toast('Arquivo ' + formato.toUpperCase() + ' gerado.', 'success');
    } catch (err) {
      F.falha('Falha na exportação', err);
    } finally {
      btn.disabled = false;
    }
  }

  $(document).on('click', '.btn-export-csv', function () { baixar(this, 'csv'); });
  $(document).on('click', '.btn-export-json', function () { baixar(this, 'json'); });
  $(document).on('click', '.btn-export:not(.btn-export-csv):not(.btn-export-json)', function () { baixar(this, 'csv'); });
})();
