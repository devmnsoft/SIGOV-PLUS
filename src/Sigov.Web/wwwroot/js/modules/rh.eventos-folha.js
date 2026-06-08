(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["folha-eventos"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("folha-eventos"); },
    exportarCsv: "/api/rh/export/folha-eventos.csv",
    exportarJson: "/api/rh/export/folha-eventos.json"
  };
})();
