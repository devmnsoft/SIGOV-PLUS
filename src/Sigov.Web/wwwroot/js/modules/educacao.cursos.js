(function () {
  window.SigovEducacaoModulo = window.SigovEducacaoModulo || {};
  window.SigovEducacaoModulo["cursos-series"] = {
    init: function () { window.sigovUi?.notify?.("Tela de Educação pronta para operação padronizada.", "info"); }
  };
  $(function () { window.SigovEducacaoModulo["cursos-series"].init(); });
})();
