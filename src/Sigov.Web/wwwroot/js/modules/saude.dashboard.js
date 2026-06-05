(function () {
  $(function () {
    var root = $('#saude-dashboard');
    $.getJSON(root.data('api')).done(function (r) {
      var d = r.data || {}; var cards = [
        ['Unidades', d.totalUnidades], ['Profissionais', d.totalProfissionais], ['Pacientes ativos', d.totalPacientesAtivos], ['Atendimentos hoje', d.atendimentosHoje], ['Agenda hoje', d.agendaHoje], ['Estoque baixo', d.estoqueBaixo], ['Visitas ACS mês', d.visitasAcsMes], ['Syncs pendentes', d.syncsPendentes]
      ];
      root.empty(); cards.forEach(function (c) { root.append('<div class="col-md-3"><div class="card shadow-sm"><div class="card-body"><div class="text-muted">' + c[0] + '</div><div class="display-6">' + (c[1] || 0) + '</div></div></div></div>'); });
    }).fail(function (xhr) { root.html('<div class="alert alert-danger">Erro ao carregar dashboard: ' + xhr.status + '</div>'); });
  });
}());
