(function ($) {
    'use strict';

    const endpoint = '/api/agro/programas';
    const targetId = 'grid-programas';

    function token() {
        return $('input[name="__RequestVerificationToken"]').val();
    }

    function renderError(xhr) {
        const msg = xhr.status === 401 ? 'Sessão expirada. Faça login novamente.'
            : xhr.status === 403 ? 'Você não possui permissão ou a feature do módulo Agro está desabilitada.'
            : xhr.status === 404 ? 'Registro não encontrado.'
            : xhr.status === 422 ? (xhr.responseJSON && xhr.responseJSON.message) || 'Verifique os dados informados.'
            : 'Não foi possível carregar Programas rurais.';
        $('#agro-alert').removeClass('d-none alert-info').addClass('alert-warning').text(msg);
    }

    function renderList(response) {
        const $target = $('#' + targetId);
        const payload = response && (response.data || response);
        const items = payload.items || [];
        if (!items.length) {
            $target.html('<div class="text-center text-muted py-4">Nenhum registro encontrado.</div>');
            return;
        }
        const rows = items.map(function (item) {
            const nome = item.nome || item.numero || item.codigo || 'Registro';
            const status = item.status || item.situacao || (item.ativo === false ? 'INATIVO' : 'ATIVO');
            return '<tr><td>' + (item.codigo || item.numero || item.id) + '</td><td>' + nome + '</td><td><span class="badge bg-secondary">' + status + '</span></td></tr>';
        }).join('');
        $target.html('<div class="table-responsive"><table class="table table-hover align-middle"><thead><tr><th>Código/Número</th><th>Descrição</th><th>Status</th></tr></thead><tbody>' + rows + '</tbody></table></div>');
    }

    function renderResumo(response) {
        const data = response && (response.data || response);
        const cards = [
            ['Máquinas ativas', data.maquinasAtivas || 0],
            ['Serviços pendentes', data.servicosPendentes || 0],
            ['Serviços agendados', data.servicosAgendados || 0],
            ['Executados no mês', data.servicosExecutadosMes || 0],
            ['Horas no mês', data.horasTrabalhadasMes || 0],
            ['Área atendida (ha)', data.areaAtendidaMes || 0]
        ].map(function (c) { return '<div class="col-md-2"><div class="card h-100"><div class="card-body"><div class="text-muted small">' + c[0] + '</div><div class="h4">' + c[1] + '</div></div></div></div>'; }).join('');
        $('#' + targetId).html('<div class="row g-3">' + cards + '</div>');
    }

    function load() {
        $.ajax({ url: endpoint, method: 'GET', headers: { 'RequestVerificationToken': token() } })
            .done(function (response) { targetId === 'cards-patrulha' ? renderResumo(response) : renderList(response); })
            .fail(renderError);
    }

    $(load);
})(jQuery);
