(function ($) {
  'use strict';
  function showError(message) { $('#agroDashboardErro').text(message || 'Não foi possível carregar o dashboard Agro.').removeClass('d-none'); }
  $(function () {
    $('#agroDashboardLoading').removeClass('d-none');
    $.ajax({ url: '/api/agro/dashboard', method: 'GET' })
      .done(function (response) {
        var data = response && response.data ? response.data : {};
        $('[data-agro-kpi="totalCamadas"]').text(data.totalCamadas || 0);
        $('[data-agro-kpi="totalFeicoes"]').text(data.totalFeicoes || 0);
        $('[data-agro-kpi="totalEventos"]').text(data.totalEventos || 0);
        $('[data-agro-kpi="totalProdutores"]').text(data.totalProdutores || 0);
        $('[data-agro-kpi="produtoresAtivos"]').text(data.produtoresAtivos || 0);
        $('[data-agro-kpi="totalPropriedades"]').text(data.totalPropriedades || 0);
        $('[data-agro-kpi="areaTotalMapeada"]').text(data.areaTotalMapeada || 0);
        $('[data-agro-kpi="areaProdutiva"]').text(data.areaProdutiva || 0);
        $('[data-agro-kpi="totalTalhoes"]').text(data.totalTalhoes || 0);
        $('[data-agro-kpi="culturasCadastradas"]').text(data.culturasCadastradas || 0);
        $('[data-agro-kpi="safrasAtivas"]').text(data.safrasAtivas || 0);
        $('[data-agro-kpi="producaoEstimada"]').text(data.producaoEstimada || 0);
        $('[data-agro-kpi="producaoRealizada"]').text(data.producaoRealizada || 0);
      })
      .fail(function (xhr) {
        var messages = { 401: 'Faça login para acessar o Dashboard Agro.', 403: 'Você não possui permissão para visualizar o Dashboard Agro.', 404: 'Dashboard Agro não encontrado.', 422: 'Verifique os filtros informados.', 500: 'Falha interna ao carregar o Dashboard Agro.' };
        showError(messages[xhr.status] || 'Falha ao carregar o Dashboard Agro.');
      })
      .always(function () { $('#agroDashboardLoading').addClass('d-none'); });
  });
})(jQuery);
