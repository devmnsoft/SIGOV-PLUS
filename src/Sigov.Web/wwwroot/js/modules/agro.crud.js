(function ($) {
    'use strict';

    const definitions = {
        programas: { endpoint: '/api/agro/programas', title: 'Programa rural', fields: [['codigo', 'Código'], ['nome', 'Nome'], ['tipoPrograma', 'Tipo do programa']] },
        beneficios: { endpoint: '/api/agro/beneficios', title: 'Benefício rural', fields: [['codigo', 'Código'], ['nome', 'Nome'], ['tipoBeneficio', 'Tipo do benefício']] },
        concessoes: { endpoint: '/api/agro/beneficios/concessoes', title: 'Concessão', fields: [['beneficioId', 'Benefício ID', 'number'], ['produtorId', 'Produtor ID', 'number'], ['quantidade', 'Quantidade', 'number']] },
        insumos: { endpoint: '/api/agro/insumos', title: 'Insumo', fields: [['codigo', 'Código'], ['nome', 'Nome'], ['tipoInsumo', 'Tipo'], ['unidadeMedida', 'Unidade']], defaults: { controlaEstoque: true } },
        distribuicoes: { endpoint: '/api/agro/insumos/distribuicoes', title: 'Distribuição', fields: [['insumoId', 'Insumo ID', 'number'], ['produtorId', 'Produtor ID', 'number'], ['quantidade', 'Quantidade', 'number']] },
        maquinas: { endpoint: '/api/agro/maquinas', title: 'Máquina', fields: [['codigo', 'Código'], ['nome', 'Nome'], ['tipoMaquina', 'Tipo'], ['situacao', 'Situação']], defaults: { ativo: true } },
        implementos: { endpoint: '/api/agro/implementos', title: 'Implemento', fields: [['codigo', 'Código'], ['nome', 'Nome'], ['tipoImplemento', 'Tipo'], ['situacao', 'Situação']], defaults: { ativo: true } },
        agenda: { endpoint: '/api/agro/maquinas/agenda', title: 'Agendamento', fields: [['maquinaId', 'Máquina ID', 'number'], ['dataInicio', 'Início', 'datetime-local'], ['dataFim', 'Fim', 'datetime-local'], ['status', 'Status']] },
        servicos: { endpoint: '/api/agro/servicos-maquina', title: 'Serviço de máquina', fields: [['produtorId', 'Produtor ID', 'number'], ['tipoServico', 'Tipo de serviço'], ['dataAgendada', 'Data agendada', 'date'], ['valorEstimado', 'Valor estimado', 'number']] }
    };

    function html(value) { return $('<div>').text(value == null ? '' : value).html(); }
    function message(text, danger) { $('#agro-alert').removeClass('d-none alert-info alert-warning alert-success').addClass(danger ? 'alert-warning' : 'alert-success').text(text); }
    function payload(response) { return response && (response.data || response); }
    function error(xhr) { message(xhr.status === 403 ? 'Você não possui permissão para esta operação.' : ((xhr.responseJSON && xhr.responseJSON.message) || 'Não foi possível concluir a operação.'), true); }

    $(function () {
        const resource = $('.agro-page').data('agro-resource');
        const definition = definitions[resource];
        if (!definition) return;
        const $grid = $('#grid-' + resource);

        function load() {
            $.getJSON(definition.endpoint).done(function (response) {
                const items = (payload(response) || {}).items || [];
                if (!items.length) return $grid.html('<div class="text-center text-muted py-4">Nenhum registro encontrado.</div>');
                $grid.html('<div class="table-responsive"><table class="table table-hover"><thead><tr><th>Código/Número</th><th>Descrição</th><th>Status</th></tr></thead><tbody>' + items.map(function (item) {
                    return '<tr><td>' + html(item.codigo || item.numero || item.id) + '</td><td>' + html(item.nome || item.tipoServico || item.produtorNomeMascarado || '-') + '</td><td>' + html(item.status || item.situacao || (item.ativo === false ? 'INATIVO' : 'ATIVO')) + '</td></tr>';
                }).join('') + '</tbody></table></div>');
            }).fail(error);
        }

        $('[data-agro-action="novo"]').on('click', function () {
            const controls = definition.fields.map(function (field) { return '<div class="col-md-6"><label class="form-label">' + html(field[1]) + '</label><input class="form-control" name="' + field[0] + '" type="' + (field[2] || 'text') + '" required></div>'; }).join('');
            const modal = '<div class="modal fade" id="agroCrudModal" tabindex="-1"><div class="modal-dialog modal-lg"><div class="modal-content"><form id="agroCrudForm"><div class="modal-header"><h2 class="modal-title h5">Novo ' + html(definition.title) + '</h2><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Fechar"></button></div><div class="modal-body"><div class="row g-3">' + controls + '</div></div><div class="modal-footer"><button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Cancelar</button><button type="submit" class="btn btn-primary">Salvar</button></div></form></div></div></div>';
            $('#agroCrudModal').remove(); $('body').append(modal);
            bootstrap.Modal.getOrCreateInstance(document.getElementById('agroCrudModal')).show();
        });

        $(document).on('submit', '#agroCrudForm', function (event) {
            event.preventDefault();
            const body = Object.assign({}, definition.defaults || {});
            definition.fields.forEach(function (field) { const value = event.currentTarget.elements[field[0]].value; body[field[0]] = field[2] === 'number' ? Number(value) : value; });
            $.ajax({ url: definition.endpoint, method: 'POST', contentType: 'application/json', data: JSON.stringify(body), headers: { RequestVerificationToken: $('input[name="__RequestVerificationToken"]').val() } })
                .done(function () { bootstrap.Modal.getInstance(document.getElementById('agroCrudModal')).hide(); message(definition.title + ' salvo com sucesso.'); load(); }).fail(error);
        });
        load();
    });
})(jQuery);
