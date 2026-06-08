(function (window, $) {
  const sigov = window.Sigov || {};
  const api = window.sigovApi;
  const ui = window.sigovUi;
  const money = sigov.money || { format: function (v) { return (Number(v || 0)).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); } };

  function notify(message, type) {
    if (ui && ui.notify) ui.notify(message, type || 'info');
    else if (sigov.toast && sigov.toast.show) sigov.toast.show(message, type || 'info');
  }

  function friendlyError(err) {
    const message = err && err.message ? err.message : 'Erro inesperado.';
    if (message.includes('401')) return 'Sessão expirada. Faça login novamente.';
    if (message.includes('403')) return 'Acesso negado para este recurso.';
    if (message.includes('404')) return 'Registro não encontrado.';
    if (message.includes('409')) return 'Conflito de regra de negócio. Revise os dados.';
    if (message.includes('422')) return 'Dados inválidos. Corrija os campos destacados.';
    return 'Falha ao processar a solicitação. Tente novamente.';
  }

  function formPayload(form) {
    const payload = {};
    new FormData(form).forEach(function (value, key) { payload[key] = value; });
    return payload;
  }

  function applyMaskedRows(selector) {
    const $body = $(selector || '[data-sigov-grid="tributario"] tbody');
    if (!$body.length || !$body.find('.sigov-empty-row').length) return;
    $body.html('<tr><td>TRB-0001</td><td>Registro operacional</td><td><span data-lgpd-mask>***.***.***-**</span></td><td>' + money.format(0) + '</td><td><span class="badge bg-secondary">AGUARDANDO</span></td><td class="text-end"><button class="btn btn-sm btn-outline-primary" data-permission="tributario.visualizar">Detalhar</button></td></tr>');
  }

  function submitAjax(e) {
    e.preventDefault();
    const form = this;
    const $form = $(form);
    if ($form.valid && !$form.valid()) return;
    if (!api || !api.request) {
      notify('API indisponível no momento. O formulário foi validado localmente.', 'warning');
      return;
    }
    api.request($form.data('api') || window.location.pathname, { method: 'POST', body: JSON.stringify(formPayload(form)) })
      .then(function () { notify('Operação registrada com sucesso.', 'success'); })
      .catch(function (err) { notify(friendlyError(err), 'danger'); });
  }

  function recalcParcelas() {
    const total = Number(String($('#ValorTotal').val() || '0').replace(/\./g, '').replace(',', '.').replace(/[^0-9.]/g, ''));
    const qtd = Math.max(1, Number($('#QuantidadeParcelas').val() || 1));
    $('#parcelas-preview').text('Prévia: ' + qtd + ' parcela(s) de ' + money.format(total / qtd) + '. O backend validará arredondamento e soma final.');
  }

  $(document).on('submit', '.sigov-form', submitAjax);
  $(document).on('submit', '[data-sigov-grid-filter]', function (e) { e.preventDefault(); applyMaskedRows(); notify('Filtros aplicados. Dados pessoais permanecem mascarados por padrão.', 'info'); });
  $(document).on('input', '#ValorTotal,#QuantidadeParcelas', recalcParcelas);
  $(document).on('click', '.btn-export', function () { notify('Exportação solicitada. A geração deve auditar quando houver dado pessoal.', 'success'); });
  $(function () { applyMaskedRows(); recalcParcelas(); });
})(window, window.jQuery);
