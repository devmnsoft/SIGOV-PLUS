(function () {
  function toast(msg, type) { $('#sigov-alerts').html(`<div class="alert alert-${type || 'info'} alert-dismissible fade show">${msg}<button type="button" class="btn-close" data-bs-dismiss="alert"></button></div>`); }
  function money(v) { return (Number(v || 0)).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' }); }
  $(document).on('input', '.item-quantidade,.item-valor', function () { let total = 0; $('.item-empenho').each(function () { const q = parseFloat(($(this).find('.item-quantidade').val() || '0').replace(',', '.')); const v = parseFloat(($(this).find('.item-valor').val() || '0').replace(',', '.')); const t = q * v; total += t; $(this).find('.item-total').val(money(t)); }); $('#valor-total-empenho').val(money(total)); });
  $(document).on('click', '#add-item-empenho', function () { const i = $('.item-empenho').length; $('#empenho-itens').append(`<div class="row g-2 item-empenho mt-1"><div class="col-md-6"><input class="form-control item-descricao" name="Itens[${i}].Descricao" required /></div><div class="col-md-2"><input class="form-control item-quantidade money" name="Itens[${i}].Quantidade" value="1" /></div><div class="col-md-2"><input class="form-control item-valor money" name="Itens[${i}].ValorUnitario" value="0" /></div><div class="col-md-2"><input class="form-control item-total" readonly /></div></div>`); });
  $(document).on('submit', '.financeiro-form', async function (e) { e.preventDefault(); const form = $(this); if (form.valid && !form.valid()) return; const payload = Object.fromEntries(new FormData(this).entries()); try { await window.sigovApi.request(form.data('api'), { method: 'POST', body: JSON.stringify(payload) }); toast('Operação financeira processada com sucesso.', 'success'); } catch (err) { toast(err.message || 'Falha ao processar.', 'danger'); } });
  $(document).on('click', '.btn-export', function () { const r = $(this).data('resource') || 'empenhos'; window.open(`http://localhost:5001/api/financeiro/export/${r}.csv`, '_blank'); });
  if ($('.financeiro-dashboard').length) {
    const status = $('#financeiro-dashboard-status');
    window.sigovApi.request('/api/financeiro/dashboard').then(r => {
      const d = r.data || r; const despesa = d.despesa || {};
      const vals = [despesa.orcamentoAutorizado, despesa.empenhado, despesa.liquidado, despesa.pago, despesa.saldoDisponivel];
      $('.financeiro-dashboard .valor').each((i, el) => $(el).removeClass('placeholder-glow').text(money(vals[i])));
      const total = Number(despesa.orcamentoAutorizado || 0);
      ['empenhado', 'liquidado', 'pago'].forEach(k => $(`[data-bar="${k}"] i`).css('width', `${total > 0 ? Math.min(100, Number(despesa[k] || 0) * 100 / total) : 0}%`));
      const alerts = [];
      if (total <= 0) alerts.push('Não há dotação autorizada no exercício atual.');
      if (Number(despesa.saldoDisponivel) < 0) alerts.push('O saldo disponível está negativo e requer conferência.');
      $('#financeiro-alertas').html(alerts.length ? `<ul class="mb-0">${alerts.map(a => `<li>${a}</li>`).join('')}</ul>` : 'Sem alertas calculáveis com os dados atuais.');
      status.text('Dados do exercício carregados.');
    }).catch(err => { status.text('Não foi possível carregar o painel.'); $('#financeiro-alertas').text('Não foi possível consultar os dados. Tente novamente ou informe o suporte.'); toast(err.message || 'Dashboard indisponível ou sem permissão.', 'warning'); });
  }
}());
