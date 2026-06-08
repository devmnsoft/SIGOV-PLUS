(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["saude-ocupacional"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("saude-ocupacional"); },
    exportarCsv: "/api/rh/export/saude-ocupacional.csv",
    exportarJson: "/api/rh/export/saude-ocupacional.json"
  };
})();
