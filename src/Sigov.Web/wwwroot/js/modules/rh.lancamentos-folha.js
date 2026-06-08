(function () {
  window.SigovRhModulo = window.SigovRhModulo || {};
  window.SigovRhModulo["folha-lancamentos"] = {
    carregar: function () { return window.SigovRh?.loadGrid?.("folha-lancamentos"); },
    exportarCsv: "/api/rh/export/folha-lancamentos.csv",
    exportarJson: "/api/rh/export/folha-lancamentos.json"
  };
})();
