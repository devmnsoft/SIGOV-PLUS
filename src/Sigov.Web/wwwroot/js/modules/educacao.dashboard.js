(function ($) {
  'use strict';
  function loadDashboard() {
    $('#education-loading').removeClass('d-none'); $('#education-content,#education-error').addClass('d-none');
    $.getJSON('/api/educacao/dashboard', $('#education-filters').serialize()).done(function (response) {
      const data = response.data || {};
      Object.keys(data).forEach(function (key) { $('[data-field="' + key + '"]').text(data[key] ?? 0); });
      const total = Number(data.vagasTotais || 0), occupied = Number(data.vagasOcupadas || 0);
      const occupancy = total > 0 ? Math.min(100, Math.round((occupied / total) * 100)) : 0;
      $('#education-occupancy').text(occupancy + '%'); $('#education-capacity-bar').css('--bar-value', occupancy);
      $('#education-capacity-copy').text(occupancy + '% da capacidade da rede está ocupada neste recorte.');
      const alerts = data.alertas || [];
      $('#educacao-alertas').html(alerts.map(function (alert) { return '<li><span aria-hidden="true">!</span><p>' + $('<div>').text(alert).html() + '</p></li>'; }).join(''));
      $('#education-empty').toggleClass('d-none', alerts.length > 0);
      $('#education-loading').addClass('d-none'); $('#education-content').removeClass('d-none');
    }).fail(function () { $('#education-loading').addClass('d-none'); $('#education-error').removeClass('d-none'); });
  }
  $(function () { loadDashboard(); $('#education-filters').on('submit', function (event) { event.preventDefault(); loadDashboard(); }); });
})(jQuery);
