(function (window, $) {
  'use strict';
  window.sigovSaneamento = {
    api: function (endpoint) { return '/api/saneamento/' + endpoint; },
    toast: function (msg, type) { if (window.SigovUi && window.SigovUi.toast) { window.SigovUi.toast(msg, type || 'info'); } else { console.log(msg); } },
    loadGrid: function (endpoint) {
      var $grid = $('#san-grid'); if (!$grid.length) return;
      endpoint = endpoint || $grid.data('endpoint'); $('.san-loading').removeClass('d-none');
      $.getJSON(this.api(endpoint), { page: 1, pageSize: 20 }).done(function (res) {
        var items = (res.data && res.data.items) || res.items || [];
        var rows = items.map(function (x) { var code = x.codigoConsumidor || x.codigoUnidade || x.numero || x.numeroLigacao || x.numeroSerie || x.codigo || ''; var status = x.situacao || x.status || ''; return '<tr><td>' + (x.id || '') + '</td><td>' + code + '</td><td><span class="badge bg-primary">' + status + '</span></td><td>Dados pessoais protegidos/LGPD</td></tr>'; });
        $grid.find('tbody').html(rows.join('') || '<tr><td colspan="4" class="text-muted">Nenhum registro encontrado.</td></tr>');
      }).fail(function (xhr) { window.sigovSaneamento.toast(xhr.status === 403 ? 'Acesso negado ao módulo Saneamento.' : 'Falha ao carregar dados.', 'danger'); })
        .always(function () { $('.san-loading').addClass('d-none'); });
    },
    wire: function (endpoint) {
      var self = this; $(function () { self.loadGrid(endpoint); $('#btn-san-refresh').on('click', function () { self.loadGrid(endpoint); }); $('.btn-san-save').on('click', function () { self.toast('Use os formulários específicos/API REST para gravação validada no backend.', 'info'); }); });
    }
  };
}(window, jQuery));
