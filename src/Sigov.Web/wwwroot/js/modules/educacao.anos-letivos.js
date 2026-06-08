(function () {
  window.SigovEducacaoModulo = window.SigovEducacaoModulo || {};
  window.SigovEducacaoModulo["anos-letivos"] = {
    init: function () { window.sigovUi?.notify?.("Tela de Educação pronta para operação padronizada.", "info"); }
  };
  $(function () { window.SigovEducacaoModulo["anos-letivos"].init(); });
})();
