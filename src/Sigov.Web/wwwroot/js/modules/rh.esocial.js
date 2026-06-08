(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["esocial"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("esocial"); },
    exportarCsv: "/api/rh/export/esocial.csv",
    exportarJson: "/api/rh/export/esocial.json"
  };
})();
