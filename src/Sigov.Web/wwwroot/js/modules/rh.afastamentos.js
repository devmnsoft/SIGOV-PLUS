(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["afastamentos"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("afastamentos"); },
    exportarCsv: "/api/rh/export/afastamentos.csv",
    exportarJson: "/api/rh/export/afastamentos.json"
  };
})();
