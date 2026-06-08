(function () {
  window.SigovEducacaoModulo = window.SigovEducacaoModulo || {};
  window.SigovEducacaoModulo["responsaveis"] = {
    init: function () { window.sigovUi?.notify?.("Tela de Educação pronta para operação padronizada.", "info"); }
  };
  $(function () { window.SigovEducacaoModulo["responsaveis"].init(); });
})();
