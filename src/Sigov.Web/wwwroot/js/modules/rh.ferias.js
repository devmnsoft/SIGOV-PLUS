(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["ferias"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("ferias"); },
    exportarCsv: "/api/rh/export/ferias.csv",
    exportarJson: "/api/rh/export/ferias.json"
  };
})();
