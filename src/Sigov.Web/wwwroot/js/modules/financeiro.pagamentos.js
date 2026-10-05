// SIGOV PLUS · Financeiro SIAFIC — Pagamentos: listagem filtrável e cancelamento (estorno)
// com prévia de recálculo em cadeia pagamento → liquidação → empenho (autoridade no servidor).
(function () {
  const F = window.sigovFin;
  if (!F || !document.querySelector('[data-fin-tela="pagamentos"]')) return;
  const SIZE = 50;
  let pagina = 1, ocupado = false, selLiq = null;

  function pagerHtml(p, totalItems) {
    const totalPag = Math.max(1, Math.ceil((totalItems || 0) / SIZE));
    return `<div class="d-flex align-items-center gap-2 mt-2">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${p <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${p} de ${totalPag} · ${totalItems || 0} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${p >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  async function carregar() {
    const tb = $('#tbody-pagamentos-lista');
    tb.html('<tr><td colspan="8" class="text-muted py-3">Carregando…</td></tr>');
    try {
      const paged = await F.getJson('/api/financeiro/pagamentos' + F.qs({
        page: pagina, pageSize: SIZE,
        numero: $('#pg-numero').val() || null,
        liquidacaoId: selLiq ? selLiq.id() : null,
        status: $('#pg-status').val() || null
      }));
      const itens = paged.items || [];
      tb.html(itens.length ? itens.map(p => `<tr>
        <td>${F.escapa(p.numero)}</td>
        <td>Liq. #${p.liquidacaoId}</td>
        <td>${F.dateStr(p.dataPagamento)}</td>
        <td>${F.escapa(p.formaPagamento)}</td>
        <td>${F.escapa(p.contaBancaria || '—')}</td>
        <td class="text-end">${F.money(p.valor)}</td>
        <td>${F.statusBadge(p.status)}</td>
        <td class="text-nowrap">${p.status === 'EFETUADO' ? `<button type="button" class="btn btn-sm btn-outline-danger btn-cancelar-pag" data-pag-id="${p.id}">Cancelar</button>` : '<span class="text-muted small">—</span>'}</td>
      </tr>`).join('') : '<tr><td colspan="8" class="text-muted py-3">Nenhum pagamento para os filtros atuais.</td></tr>');
      $('#pager-pagamentos').html(pagerHtml(pagina, paged.totalItems));
    } catch (err) { F.falha('Falha ao carregar os pagamentos', err); }
  }

  async function cancelar(btn) {
    if (ocupado) return;
    ocupado = true;
    try {
      const id = Number(btn.getAttribute('data-pag-id'));
      const p = await F.getJson(`/api/financeiro/pagamentos/${id}`);
      if (p.status !== 'EFETUADO') { F.toast('Este pagamento já está cancelado.', 'warning'); return; }
      const l = await F.getJson(`/api/financeiro/liquidacoes/${p.liquidacaoId}`);
      const d = await F.getJson(`/api/financeiro/empenhos/${l.empenhoId}`);
      const pagoApós = Math.max(0, d.valorPago - p.valor);
      const statusApós = F.calc.statusEmpenho(d.valorTotal, d.valorAnulado, d.valorLiquidado, pagoApós);
      F.confirmarReversao({
        titulo: `Cancelar pagamento ${F.escapa(p.numero)}`,
        linhas: [
          { rotulo: 'Valor do estorno', valor: F.money(p.valor) },
          { rotulo: 'Pago do empenho após', valor: F.money(pagoApós) },
          { rotulo: 'Saldo a pagar após', valor: F.money(F.calc.saldoAPagar(d.valorLiquidado, pagoApós)) },
          { rotulo: 'Status do empenho após', valor: statusApós }
        ],
        campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo do cancelamento (obrigatório)', obrigatorio: true }],
        pergunta: 'O cancelamento reverte o acumulado pago, devolve o saldo a pagar e vincula o estorno à origem na auditoria com data, usuário e correlation ID.',
        onConfirm: async v => {
          try {
            const body = { motivo: v.motivo };
            const chave = F.chaveAcao('fin.pagamento.cancelar:' + id, body);
            await F.postJson(`/api/financeiro/pagamentos/${id}/cancelar`, body, chave);
            F.concluirAcao('fin.pagamento.cancelar:' + id, body);
            F.toast('Pagamento cancelado (estorno registrado).', 'success');
            carregar();
            return true;
          } catch (err) { F.falha('Não foi possível cancelar o pagamento', err); return false; }
        }
      });
    } catch (err) { F.falha('Não foi possível preparar o cancelamento do pagamento', err); }
    finally { ocupado = false; }
  }

  (async function init() {
    await F.carregarCatalogos();
    selLiq = F.selectSearch($('#pg-liquidacao'), termo => F.getJson('/api/financeiro/liquidacoes' + F.qs({ numero: termo, pageSize: 8 }))
      .then(p => (p.items || []).map(x => ({ id: x.id, label: x.numero, extra: 'liquidação #' + x.id })))
      .catch(() => []), 'Busque o número da liquidação…');
    $('#btn-pg-filtrar').on('click', () => { pagina = 1; carregar(); });
    $('#btn-pg-limpar').on('click', () => {
      $('#pg-numero').val('');
      $('#pg-liquidacao').val('').removeData('fin-ok').removeData('fin-id');
      $('#pg-status').val('');
      pagina = 1; carregar();
    });
    $('#pager-pagamentos').on('click', '.pager-prev', () => { if (pagina > 1) { pagina--; carregar(); } });
    $('#pager-pagamentos').on('click', '.pager-next', () => { pagina++; carregar(); });
    $('#tbody-pagamentos-lista').on('click', '.btn-cancelar-pag', function () { cancelar(this); });
    carregar();
  })();
})();
