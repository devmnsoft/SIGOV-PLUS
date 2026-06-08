(function ($) {
    'use strict';
    $(function () {
        $('#planoCodigo').val($('#planoInicial').val());
        $('#formCadastroCliente').on('submit', function (e) {
            e.preventDefault();
            const form = $(this);
            const data = Object.fromEntries(new FormData(this).entries());
            data.modulosInteresse = (data.modulosInteresse || '').split(',').map(x => x.trim()).filter(Boolean);
            data.usuariosEstimados = data.usuariosEstimados ? parseInt(data.usuariosEstimados, 10) : null;
            data.desejaWhiteLabel = form.find('[name=desejaWhiteLabel]').is(':checked');
            data.desejaDominioCustomizado = form.find('[name=desejaDominioCustomizado]').is(':checked');
            data.aceiteTermos = form.find('[name=aceiteTermos]').is(':checked');
            $.ajax({ url: '/api/publico/cadastro-cliente', method: 'POST', contentType: 'application/json', data: JSON.stringify(data) })
                .done(r => { $('#cadastroClienteMensagem').html(`<div class="alert alert-success">Solicitação recebida. Protocolo ${r.data.protocolo}</div>`); })
                .fail(xhr => { $('#cadastroClienteMensagem').html(`<div class="alert alert-danger">${xhr.responseJSON?.errors?.join('<br>') || 'Revise os campos.'}</div>`); });
        });
    });
})(jQuery);
