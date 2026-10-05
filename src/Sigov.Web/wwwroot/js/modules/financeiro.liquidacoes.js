// SIGOV PLUS · Financeiro SIAFIC — Liquidações: listagem filtrável e anulação (estorno)
// com prévia de recálculo. Servidor é autoridade: o estado vigente é reconsultado antes da confirmação.
(function () {
  const F = window.sigovFin;
  if (!F || !document.querySelector('[data-fin-tela="liquidacoes"]')) return;
  const SIZE = 50;
  let pagina = 1, ocupado = false, selEmpenho = null;

  function pagerHtml(p, totalItems) {
    const totalPag = Math.max(1, Math.ceil((totalItems || 0) / SIZE));
    return `<div class="d-flex align-items-center gap-2 mt-2">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${p <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${p} de ${totalPag} · ${totalItems || 0} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${p >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  async function carregar() {
    const tb = $('#tbody-liquidacoes-lista');
    tb.html('<tr><td colspan="8" class="text-muted py-3">Carregando…</td></tr>');
    try {
      const paged = await F.getJson('/api/financeiro/liquidacoes' + F.qs({
        page: pagina, pageSize: SIZE,
        numero: $('#fil-numero').val() || null,
        empenhoId: selEmpenho ? selEmpenho.id() : null,
        status: $('#fil-status').val() || null
      }));
      const itens = paged.items || [];
      tb.html(itens.length ? itens.map(l => `<tr>
        <td>${F.escapa(l.numero)}</td>
        <td><a href="/Financeiro/EmpenhoDetalhe/${l.empenhoId}">#${l.empenhoId}</a></td>
        <td>${F.dateStr(l.dataLiquidacao)}</td>
        <td>${F.escapa(l.documentoFiscal || '—')}</td>
        <td>${F.escapa(l.historico || '—')}</td>
        <td class="text-end">${F.money(l.valor)}</td>
        <td>${F.statusBadge(l.status)}</td>
        <td class="text-nowrap">${l.status === 'LIQUIDADA' ? `<button type="button" class="btn btn-sm btn-outline-warning btn-anular-liq" data-liq-id="${l.id}" data-valor="${l.valor}">Anular</button>` : '<span class="text-muted small">—</span>'}</td>
      </tr>`).join('') : '<tr><td colspan="8" class="text-muted py-3">Nenhuma liquidação para os filtros atuais.</td></tr>');
      $('#pager-liquidacoes').html(pagerHtml(pagina, paged.totalItems));
    } catch (err) { F.falha('Falha ao carregar as liquidações', err); }
  }

  async function anular(btn) {
    if (ocupado) return;
    ocupado = true;
    try {
      const id = Number(btn.getAttribute('data-liq-id'));
      const l = await F.getJson(`/api/financeiro/liquidacoes/${id}`);
      const d = await F.getJson(`/api/financeiro/empenhos/${l.empenhoId}`);
      if (l.status !== 'LIQUIDADA') { F.toast('Esta liquidação já não está vigente.', 'warning'); return; }
      const liqApós = Math.max(0, d.valorLiquidado - l.valor);
      const statusApós = F.calc.statusEmpenho(d.valorTotal, d.valorAnulado, liqApós, d.valorPago);
      F.confirmarReversao({
        titulo: `Anular liquidação ${F.escapa(l.numero)}`,
        linhas: [
          { rotulo: 'Valor da liquidação', valor: F.money(l.valor) },
          { rotulo: 'Liquidado do empenho após', valor: F.money(liqApós) },
          { rotulo: 'Saldo a pagar após', valor: F.money(F.calc.saldoAPagar(liqApós, d.valorPago)) },
          { rotulo: 'Status do empenho após', valor: statusApós }
        ],
        campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo da anulação (obrigatório)', obrigatorio: true }],
        pergunta: 'A anulação recomputa todos os acumuladores do empenho no servidor, bloqueia o que for incompatível e registra auditoria com data, usuário e correlation ID.',
        onConfirm: async v => {
          try {
            const body = { motivo: v.motivo };
            const chave = F.chaveAcao('fin.liquidacao.anular:' + id, body);
            await F.postJson(`/api/financeiro/liquidacoes/${id}/anular`, body, chave);
            F.concluirAcao('fin.liquidacao.anular:' + id, body);
            F.toast('Liquidação anulada.', 'success');
            carregar();
            return true;
          } catch (err) { F.falha('Não foi possível anular a liquidação', err); return false; }
        }
      });
    } catch (err) { F.falha('Não foi possível preparar a anulação da liquidação', err); }
    finally { ocupado = false; }
  }

  (async function init() {
    await F.carregarCatalogos();
    selEmpenho = F.selectSearch($('#fil-empenho'), termo => F.getJson('/api/financeiro/empenhos' + F.qs({ numero: termo, pageSize: 8 }))
      .then(p => (p.items || []).map(x => ({ id: x.id, label: x.numero, extra: x.fornecedor || undefined })))
      .catch(() => []), 'Busque o número do empenho…');
    $('#btn-fil-filtrar').on('click', () => { pagina = 1; carregar(); });
    $('#btn-fil-limpar').on('click', () => {
      $('#fil-numero').val('');
      $('#fil-empenho').val('').removeData('fin-ok').removeData('fin-id');
      $('#fil-status').val('');
      pagina = 1; carregar();
    });
    $('#pager-liquidacoes').on('click', '.pager-prev', () => { if (pagina > 1) { pagina--; carregar(); } });
    $('#pager-liquidacoes').on('click', '.pager-next', () => { pagina++; carregar(); });
    $('#tbody-liquidacoes-lista').on('click', '.btn-anular-liq', function () { anular(this); });
    carregar();
  })();
})();
