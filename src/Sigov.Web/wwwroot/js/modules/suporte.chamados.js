(function (window, $) {
  'use strict';

  function bindSupportForm() {
    $('form[data-sigov-form="true"]').on('submit.sigov-support', function (event) {
      event.preventDefault();
      if (window.SigovToast) {
        window.SigovToast.info('Chamado piloto validado. A integração da API de suporte será habilitada no próximo lote.');
      }
    });
  }

  function bindSupportGrid() {
    $('[data-sigov-grid="chamados-suporte"] [data-sigov-grid-search="true"]').on('input', function () {
      var term = String($(this).val() || '').toLowerCase();
      $('[data-sigov-empty-row="true"]').toggle(term.length === 0 || 'central pronta para operação assistida'.indexOf(term) >= 0);
    });
  }

  $(function () {
    bindSupportForm();
    bindSupportGrid();
  });
})(window, window.jQuery);
