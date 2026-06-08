(function () {
  window.SigovEducacaoModulo = window.SigovEducacaoModulo || {};
  window.SigovEducacaoModulo["notas"] = {
    init: function () { window.sigovUi?.notify?.("Tela de Educação pronta para operação padronizada.", "info"); }
  };
  $(function () { window.SigovEducacaoModulo["notas"].init(); });
})();
