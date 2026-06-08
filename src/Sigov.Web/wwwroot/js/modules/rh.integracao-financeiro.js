(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["folhas"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("folhas"); },
    exportarCsv: "/api/rh/export/folhas.csv",
    exportarJson: "/api/rh/export/folhas.json"
  };
})();
