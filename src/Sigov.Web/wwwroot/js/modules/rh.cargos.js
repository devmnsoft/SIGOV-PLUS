(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["cargos"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("cargos"); },
    exportarCsv: "/api/rh/export/cargos.csv",
    exportarJson: "/api/rh/export/cargos.json"
  };
})();
