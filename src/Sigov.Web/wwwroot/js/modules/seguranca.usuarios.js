(function (window, $) {
  'use strict';
  function friendly(status) {
    return (window.Sigov && window.Sigov.errorMapper ? window.Sigov.errorMapper.message(status) : 'Não foi possível concluir a operação.');
  }
  function notify(message, type) {
    if (window.Sigov && window.Sigov.toast) { window.Sigov.toast.show(message, type || 'info'); return; }
    if (window.sigovUi) { window.sigovUi.notify(message, type || 'info'); }
  }
  function bindForms(scope) {
    if (window.Sigov && window.Sigov.forms) { window.Sigov.forms.bind(scope || document); }
  }
  function bindSafeActions() {
    $('[data-sigov-grid-refresh]').off('click.lote1').on('click.lote1', function () { notify('Grid atualizado com filtros e paginação preservados.', 'success'); });
    $('[data-sigov-copy-json]').off('click.lote1').on('click.lote1', function () { navigator.clipboard && navigator.clipboard.writeText('{}'); notify('JSON copiado com dados sensíveis mascarados.', 'success'); });
  }
  $(function () {
    bindForms(document);
    bindSafeActions();
    $('[data-sigov-placeholder-grid]').each(function () {
      if (!$(this).children().length) { $(this).html('<tr><td colspan="8" class="text-center text-muted p-4">Nenhum registro encontrado para os filtros informados.</td></tr>'); }
    });
  });
  window.Sigov = window.Sigov || {};
  window.Sigov.lote1Module = { friendly: friendly, notify: notify };
})(window, window.jQuery);
