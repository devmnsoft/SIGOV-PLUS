(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["lotacoes"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("lotacoes"); },
    exportarCsv: "/api/rh/export/lotacoes.csv",
    exportarJson: "/api/rh/export/lotacoes.json"
  };
})();
