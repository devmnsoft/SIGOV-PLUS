(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["vinculos"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("vinculos"); },
    exportarCsv: "/api/rh/export/vinculos.csv",
    exportarJson: "/api/rh/export/vinculos.json"
  };
})();
