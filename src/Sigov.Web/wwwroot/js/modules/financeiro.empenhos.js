// SIGOV PLUS · Financeiro SIAFIC — Empenhos: listagem, criação e detalhe operacional.
// Contextos identificados pelo atributo data-fin-tela: "lista" | "criar" | "detalhe".
(function () {
  const F = window.sigovFin;
  if (!F) return;
  const telaEl = document.querySelector('[data-fin-tela]');
  const tela = telaEl ? telaEl.getAttribute('data-fin-tela') : null;
  const SIZE = 50;

  function pagerHtml(pagina, totalItems) {
    const totalPag = Math.max(1, Math.ceil((totalItems || 0) / SIZE));
    return `<div class="d-flex align-items-center gap-2 mt-2">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${pagina <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${pagina} de ${totalPag} · ${totalItems || 0} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${pagina >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  // ============================================================ LISTAGEM
  function iniciarLista() {
    let pagina = 1;
    const selects = {};

    async function carregar() {
      const tb = $('#tbody-empenhos');
      tb.html('<tr><td colspan="10" class="text-muted py-3">Carregando…</td></tr>');
      try {
        const paged = await F.getJson('/api/financeiro/empenhos' + F.qs({
          page: pagina, pageSize: SIZE,
          numero: $('#fin-lst-numero').val() || null,
          fornecedor: $('#fin-lst-fornecedor').val() || null,
          status: $('#fin-lst-status').val() || null,
          inicio: $('#fin-lst-inicio').val() || null,
          fim: $('#fin-lst-fim').val() || null,
          naturezaDespesaId: selects.natureza.id()
        }));
        const itens = paged.items || [];
        tb.html(itens.length ? itens.map(i => `<tr>
          <td><a href="/Financeiro/EmpenhoDetalhe/${i.id}">${F.escapa(i.numero)}</a></td>
          <td>${F.dateStr(i.dataEmpenho)}</td>
          <td>${F.escapa(i.fornecedor || '—')}</td>
          <td>${F.escapa(i.natureza || '—')}</td>
          <td>${F.escapa(i.fonte || '—')}</td>
          <td class="text-end">${F.money(i.valorTotal)}</td>
          <td class="text-end">${F.money(i.valorLiquidado)}</td>
          <td class="text-end">${F.money(i.valorPago)}</td>
          <td class="text-end ${Number(i.saldo) < 0 ? 'text-danger fw-semibold' : ''}">${F.money(i.saldo)}</td>
          <td>${F.statusBadge(i.status)}<br><a class="small" href="/Financeiro/EmpenhoDetalhe/${i.id}">Abrir →</a></td>
        </tr>`).join('') : '<tr><td colspan="10" class="text-muted py-3">Nenhum empenho para os filtros atuais.</td></tr>');
        $('#pager-empenhos').html(pagerHtml(pagina, paged.totalItems));
      } catch (err) { F.falha('Falha ao carregar empenhos', err); }
    }

    (async function init() {
      await F.carregarCatalogos();
      selects.fornecedor = F.selectSearch($('#fin-lst-fornecedor'), termo => F.buscarPessoas(termo));
      selects.natureza = F.selectSearch($('#fin-lst-natureza'), termo => F.buscarCatalogo('naturezaDespesa', termo));
      $('#btn-lst-filtrar').on('click', () => { pagina = 1; carregar(); });
      $('#btn-lst-limpar').on('click', () => {
        $('#fin-lst-numero').val(''); $('#fin-lst-status').val(''); $('#fin-lst-inicio').val(''); $('#fin-lst-fim').val('');
        ['#fin-lst-fornecedor', '#fin-lst-natureza'].forEach(s => $(s).val('').removeData('fin-ok').removeData('fin-id'));
        pagina = 1; carregar();
      });
      $('#pager-empenhos').on('click', '.pager-prev', () => { if (pagina > 1) { pagina--; carregar(); } });
      $('#pager-empenhos').on('click', '.pager-next', () => { pagina++; carregar(); });
      carregar();
    })();
  }

  // ============================================================ CRIAÇÃO
  function linhaItem() {
    return `<div class="row g-2 mb-2 item-linha align-items-end">
      <div class="col-md-5"><input type="text" class="form-control form-control-sm nova-desc" placeholder="Descrição do bem ou serviço"></div>
      <div class="col-md-2"><input type="number" min="0" step="0.0001" class="form-control form-control-sm nova-qtd" placeholder="Qtd."></div>
      <div class="col-md-2"><input type="text" class="form-control form-control-sm nova-vu" placeholder="Valor unitário"></div>
      <div class="col-md-2"><input type="text" class="form-control form-control-sm nova-linha-total" readonly value="R$ 0,00"></div>
      <div class="col-md-1 d-grid"><button type="button" class="btn btn-outline-danger btn-sm btn-remover-item" aria-label="Remover item">×</button></div>
    </div>`;
  }

  function iniciarCriar() {
    const form = document.getElementById('form-empenho');
    if (!form) return;
    const selects = {};

    function recalc() {
      let total = 0;
      document.querySelectorAll('#nova-itens .item-linha').forEach(l => {
        const q = parseFloat(l.querySelector('.nova-qtd').value || '0') || 0;
        const vu = F.parseMoney(l.querySelector('.nova-vu').value) || 0;
        const lt = q * vu;
        l.querySelector('.nova-linha-total').value = F.money(lt);
        total += lt;
      });
      document.getElementById('nova-valor-total').value = F.money(total);
      return total;
    }

    (async function init() {
      await F.carregarCatalogos();
      selects.dotacao = F.selectSearch($('#fin-nova-dotacao'), termo => F.buscarDotacaoDespesa(termo));
      selects.fornecedor = F.selectSearch($('#fin-nova-fornecedor'), termo => F.buscarPessoas(termo));
      document.getElementById('nova-data').value = F.todayStr();

      document.getElementById('btn-add-item').addEventListener('click', () => {
        document.getElementById('nova-itens').insertAdjacentHTML('beforeend', linhaItem());
        recalc();
      });
      document.getElementById('nova-itens').addEventListener('input', recalc);
      document.getElementById('nova-itens').addEventListener('click', ev => {
        const b = ev.target.closest('.btn-remover-item');
        if (!b) return;
        if (document.querySelectorAll('#nova-itens .item-linha').length > 1) b.closest('.item-linha').remove();
        recalc();
      });
      document.getElementById('nova-itens').insertAdjacentHTML('beforeend', linhaItem());
      recalc();

      $(form).on('submit', async ev => {
        ev.preventDefault();
        const dotacaoId = selects.dotacao.id();
        const fornecedorId = selects.fornecedor.id();
        const dataEmpenho = document.getElementById('nova-data').value;
        const historico = document.getElementById('nova-historico').value.trim();
        const tipoEmpenho = document.getElementById('nova-tipo').value;
        const observacoes = document.getElementById('nova-obs').value.trim() || null;
        const itens = [];
        let inconsistente = false;
        document.querySelectorAll('#nova-itens .item-linha').forEach(l => {
          const desc = l.querySelector('.nova-desc').value.trim();
          const qtd = parseFloat(l.querySelector('.nova-qtd').value || '0') || 0;
          const vu = F.parseMoney(l.querySelector('.nova-vu').value);
          const preenchida = desc !== '' || (l.querySelector('.nova-qtd').value || '') !== '' || (l.querySelector('.nova-vu').value || '').trim() !== '';
          if (desc && qtd > 0 && vu > 0) itens.push({ descricao: desc, quantidade: qtd, valorUnitario: vu });
          else if (preenchida) inconsistente = true;
        });
        if (inconsistente) return F.toast('Revise os itens: descrição, quantidade e valor unitário devem ser informados juntos.', 'warning');
        if (!itens.length) return F.toast('Inclua ao menos um item válido no empenho.', 'warning');
        if (!dotacaoId) return F.toast('Selecione a dotação orçamentária (busque por nome).', 'warning');
        if (!fornecedorId) return F.toast('Selecione o fornecedor (busque pelo nome).', 'warning');
        if (!dataEmpenho) return F.toast('Informe a data do empenho.', 'warning');
        if (!historico) return F.toast('O histórico do empenho é obrigatório.', 'warning');
        const btn = form.querySelector('button[type="submit"]');
        btn.disabled = true;
        try {
          const body = { orcamentoDespesaId: dotacaoId, dataEmpenho, fornecedorPessoaId: fornecedorId, historico, tipoEmpenho, itens, observacoes };
          const chave = F.chaveAcao('fin.empenho.criar', body);
          const id = await F.postJson('/api/financeiro/empenhos', body, chave);
          F.concluirAcao('fin.empenho.criar', body);
          F.toast('Empenho emitido.', 'success');
          setTimeout(() => { location.href = '/Financeiro/EmpenhoDetalhe/' + id; }, 400);
        } catch (err) {
          F.falha('Não foi possível emitir o empenho', err);
          btn.disabled = false;
        }
      });
    })();
  }

  // ============================================================ DETALHE
  function iniciarDetalhe() {
    const root = document.querySelector('[data-fin-tela="detalhe"]');
    const idEmpe = Number(root.getAttribute('data-empenho-id'));
    let atual = null;
    let ocupado = false;

    function labelDotacao(id) {
      const lista = (window.__finCats && window.__finCats.orcamentoDespesas) || [];
      const item = lista.find(i => i.id === id);
      return item ? F.labelDotacao(item) : 'Dotação #' + id;
    }

    function stat(rotulo, v, danger) {
      return `<div class="col-6 col-md-4 col-xl"><div class="border rounded p-2 h-100 bg-light"><div class="small text-muted">${rotulo}</div><div class="fw-semibold ${danger ? 'text-danger' : ''}">${F.money(v)}</div></div></div>`;
    }

    async function carregar() {
      try {
        const [d, liq] = await Promise.all([
          F.getJson(`/api/financeiro/empenhos/${idEmpe}`),
          F.getJson('/api/financeiro/liquidacoes' + F.qs({ empenhoId: idEmpe, pageSize: 50 }))
        ]);
        const itensLiq = liq.items || [];
        const pagMap = {};
        for (const l of itensLiq) {
          try {
            const p = await F.getJson('/api/financeiro/pagamentos' + F.qs({ liquidacaoId: l.id, pageSize: 50 }));
            pagMap[l.id] = p.items || [];
          } catch { pagMap[l.id] = []; }
        }
        render(d, itensLiq, pagMap);
      } catch (err) { F.falha('Não foi possível carregar o empenho', err); }
    }

    function render(d, liq, pagMap) {
      atual = d;
      const salLiq = F.calc.saldoALiquidar(d.valorTotal, d.valorAnulado, d.valorLiquidado);
      const salPag = F.calc.saldoAPagar(d.valorLiquidado, d.valorPago);
      const vigente = d.valorTotal - d.valorAnulado;

      document.getElementById('dt-titulo').innerHTML = `Empenho ${F.escapa(d.numero)} ${F.statusBadge(d.status)}`;
      document.getElementById('dt-dados').innerHTML = `
        <dl class="row g-2 small mb-0">
          <div class="col-md-3"><dt class="text-muted">Data do empenho</dt><dd class="mb-0 fw-semibold">${F.dateStr(d.dataEmpenho)}</dd></div>
          <div class="col-md-3"><dt class="text-muted">Fornecedor</dt><dd class="mb-0 fw-semibold">${F.escapa(d.fornecedor || '—')}</dd></div>
          <div class="col-md-3"><dt class="text-muted">Tipo</dt><dd class="mb-0 fw-semibold">${F.escapa(d.tipoEmpenho || '—')}</dd></div>
          <div class="col-md-3"><dt class="text-muted">Dotação orçamentária</dt><dd class="mb-0 fw-semibold">${F.escapa(labelDotacao(d.orcamentoDespesaId))}</dd></div>
          <div class="col-12"><dt class="text-muted">Histórico</dt><dd class="mb-0">${F.escapa(d.historico || '—')}</dd></div>
        </dl>`;
      document.getElementById('dt-saldos').innerHTML = `
        <div class="row g-2 text-center">
          ${stat('Valor total', d.valorTotal)}
          ${stat('Anulado', d.valorAnulado)}
          ${stat('Líquido vigente', vigente)}
          ${stat('Liquidado', d.valorLiquidado)}
          ${stat('Pago', d.valorPago)}
          ${stat('Saldo a liquidar', salLiq, salLiq < 0)}
          ${stat('Saldo a pagar', salPag, salPag < 0)}
        </div>
        <p class="form-text mt-2">Saldo a liquidar = Total − Anulado − Liquidado · Saldo a pagar = Liquidado − Pago (fórmulas centrais recalculadas pelo servidor).</p>`;
      document.getElementById('tbody-itens').innerHTML = (d.itens || []).map(it =>
        `<tr><td>${F.escapa(it.descricao)}</td><td class="text-end">${Number(it.quantidade).toLocaleString('pt-BR')}</td><td class="text-end">${F.money(it.valorUnitario)}</td><td class="text-end">${F.money(it.valorTotal)}</td></tr>`
      ).join('') || '<tr><td colspan="4" class="text-muted py-3">Sem itens.</td></tr>';

      let html = '';
      for (const l of liq) {
        const pagos = pagMap[l.id] || [];
        const pagoEfetivo = pagos.filter(p => p.status === 'EFETUADO').reduce((s, p) => s + Number(p.valor), 0);
        const saldoLiqItem = l.status === 'LIQUIDADA' ? l.valor - pagoEfetivo : 0;
        const acoes = [];
        if (l.status === 'LIQUIDADA' && saldoLiqItem > 0) acoes.push(`<button type="button" class="btn btn-sm btn-primary btn-pagar" data-liq-id="${l.id}" data-saldo="${saldoLiqItem}">Pagar</button>`);
        if (l.status === 'LIQUIDADA') acoes.push(`<button type="button" class="btn btn-sm btn-outline-warning btn-anular-liq" data-liq-id="${l.id}" data-valor="${l.valor}">Anular liquidação</button>`);
        html += `<tr><td>${F.escapa(l.numero)}</td><td>${F.dateStr(l.dataLiquidacao)}</td><td>${F.escapa(l.documentoFiscal || '—')}</td><td>${F.escapa(l.historico || '—')}</td><td class="text-end">${F.money(l.valor)}</td><td>${F.statusBadge(l.status)}</td><td class="text-nowrap">${acoes.join(' ') || '<span class="text-muted small">—</span>'}</td></tr>`;
        if (pagos.length) {
          html += '<tr class="table-light"><td colspan="7" class="ps-4"><div class="d-flex flex-column gap-2">' + pagos.map(p =>
            `<div class="d-flex justify-content-between align-items-center flex-wrap gap-2"><span class="small"><strong>${F.escapa(p.numero)}</strong> · ${F.dateStr(p.dataPagamento)} · ${F.escapa(p.formaPagamento)}${p.contaBancaria ? ' · conta ' + F.escapa(p.contaBancaria) : ''} · <strong>${F.money(p.valor)}</strong></span>` +
            `<span class="text-nowrap">${F.statusBadge(p.status)} ${p.status === 'EFETUADO' ? `<button type="button" class="btn btn-sm btn-outline-danger btn-cancelar-pag" data-pag-id="${p.id}" data-valor="${p.valor}">Cancelar pagamento</button>` : ''}</span></div>`
          ).join('') + '</div></td></tr>';
        }
      }
      document.getElementById('tbody-liquidacoes').innerHTML = html || '<tr><td colspan="7" class="text-muted py-3">Nenhuma liquidação registrada.</td></tr>';

      const evts = [{ data: d.dataEmpenho, txt: `Empenho ${d.numero} emitido (${F.money(d.valorTotal)})` }];
      liq.forEach(l => {
        evts.push({ data: l.dataLiquidacao, txt: `Liquidação ${l.numero} · ${l.status} · ${F.money(l.valor)}` });
        (pagMap[l.id] || []).forEach(p => evts.push({ data: p.dataPagamento, txt: `Pagamento ${p.numero} · ${p.status} · ${F.escapa(p.formaPagamento)} · ${F.money(p.valor)}` }));
      });
      evts.sort((a, b) => String(b.data).localeCompare(String(a.data)));
      document.getElementById('lista-historico').innerHTML = evts.map(e =>
        `<li class="list-group-item d-flex justify-content-between gap-2"><span class="me-2 text-muted small">${F.dateStr(e.data)}</span><span class="small">${F.escapa(e.txt)}</span></li>`
      ).join('');

      document.getElementById('btn-liquidar').disabled = !(salLiq > 0) || d.status === 'ANULADO';
      document.getElementById('btn-anular').disabled = d.status === 'ANULADO' || !(vigente > 0);
    }

    const $liq = $('#tbody-liquidacoes');

    $liq.on('click', '.btn-pagar', function () {
      if (ocupado || !atual) return;
      const saldo = Number(this.getAttribute('data-saldo'));
      document.getElementById('fin-pag-liquidacao-id').value = this.getAttribute('data-liq-id');
      document.getElementById('hint-saldo-pagar').textContent = `Saldo a pagar desta liquidação: ${F.money(saldo)}`;
      document.getElementById('pag-valor').value = '';
      document.getElementById('pag-valor').setAttribute('max', String(saldo));
      new bootstrap.Modal(document.getElementById('modalPagar')).show();
    });

    $liq.on('click', '.btn-anular-liq', async function () {
      if (ocupado) return;
      ocupado = true;
      try {
        const liqId = Number(this.getAttribute('data-liq-id'));
        const [l, d] = await Promise.all([
          F.getJson(`/api/financeiro/liquidacoes/${liqId}`),
          F.getJson(`/api/financeiro/empenhos/${idEmpe}`)
        ]);
        if (l.status !== 'LIQUIDADA') { F.toast('Esta liquidação já não está vigente.', 'warning'); return; }
        const liqApós = Math.max(0, d.valorLiquidado - l.valor);
        const statusApós = F.calc.statusEmpenho(d.valorTotal, d.valorAnulado, liqApós, d.valorPago);
        F.confirmarReversao({
          titulo: `Anular liquidação ${l.numero}`,
          linhas: [
            { rotulo: 'Valor da liquidação anulada', valor: F.money(l.valor) },
            { rotulo: 'Liquidado do empenho após', valor: F.money(liqApós) },
            { rotulo: 'Saldo a pagar após', valor: F.money(F.calc.saldoAPagar(liqApós, d.valorPago)) },
            { rotulo: 'Status do empenho após', valor: statusApós }
          ],
          campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo da anulação (obrigatório)', obrigatorio: true }],
          pergunta: 'Estorno de liquidação: o servidor recomputa todos os acumuladores, bloqueia pagamentos compatíveis e registra auditoria com data, usuário e correlation ID.',
          onConfirm: async v => {
            try {
              const body = { motivo: v.motivo };
              const chave = F.chaveAcao('fin.liquidacao.anular:' + liqId, body);
              await F.postJson(`/api/financeiro/liquidacoes/${liqId}/anular`, body, chave);
              F.concluirAcao('fin.liquidacao.anular:' + liqId, body);
              F.toast('Liquidação anulada.', 'success');
              carregar();
              return true;
            } catch (err) { F.falha('Não foi possível anular a liquidação', err); return false; }
          }
        });
      } catch (err) { F.falha('Não foi possível preparar a anulação da liquidação', err); }
      finally { ocupado = false; }
    });

    $liq.on('click', '.btn-cancelar-pag', async function () {
      if (ocupado) return;
      ocupado = true;
      try {
        const pagId = Number(this.getAttribute('data-pag-id'));
        const [p, d] = await Promise.all([
          F.getJson(`/api/financeiro/pagamentos/${pagId}`),
          F.getJson(`/api/financeiro/empenhos/${idEmpe}`)
        ]);
        if (p.status !== 'EFETUADO') { F.toast('Este pagamento já está cancelado.', 'warning'); return; }
        const pagoApós = Math.max(0, d.valorPago - p.valor);
        const statusApós = F.calc.statusEmpenho(d.valorTotal, d.valorAnulado, d.valorLiquidado, pagoApós);
        F.confirmarReversao({
          titulo: `Cancelar pagamento ${p.numero}`,
          linhas: [
            { rotulo: 'Valor do estorno', valor: F.money(p.valor) },
            { rotulo: 'Pago do empenho após', valor: F.money(pagoApós) },
            { rotulo: 'Saldo a pagar após', valor: F.money(F.calc.saldoAPagar(d.valorLiquidado, pagoApós)) },
            { rotulo: 'Status do empenho após', valor: statusApós }
          ],
          campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo do cancelamento (obrigatório)', obrigatorio: true }],
          pergunta: 'O estorno reverte o acumulado pago, recalcula o status do empenho e vincula a reversão à origem na auditoria.',
          onConfirm: async v => {
            try {
              const body = { motivo: v.motivo };
              const chave = F.chaveAcao('fin.pagamento.cancelar:' + pagId, body);
              await F.postJson(`/api/financeiro/pagamentos/${pagId}/cancelar`, body, chave);
              F.concluirAcao('fin.pagamento.cancelar:' + pagId, body);
              F.toast('Pagamento cancelado (estorno registrado).', 'success');
              carregar();
              return true;
            } catch (err) { F.falha('Não foi possível cancelar o pagamento', err); return false; }
          }
        });
      } catch (err) { F.falha('Não foi possível preparar o cancelamento do pagamento', err); }
      finally { ocupado = false; }
    });

    // ---------------- liquidar
    document.getElementById('btn-liquidar').addEventListener('click', () => {
      if (ocupado || !atual) return;
      const salLiq = F.calc.saldoALiquidar(atual.valorTotal, atual.valorAnulado, atual.valorLiquidado);
      document.getElementById('hint-saldo-liquidar').textContent = `Saldo a liquidar do empenho: ${F.money(salLiq)}`;
      document.getElementById('liq-valor').value = '';
      document.getElementById('liq-valor').setAttribute('max', String(salLiq));
      new bootstrap.Modal(document.getElementById('modalLiquidar')).show();
    });

    $('#form-modal-liquidar').on('submit', async ev => {
      ev.preventDefault();
      const f = new FormData(ev.target);
      const valor = Number(f.get('valor'));
      const historico = (f.get('historico') || '').toString().trim();
      if (!atual) return;
      const salLiq = F.calc.saldoALiquidar(atual.valorTotal, atual.valorAnulado, atual.valorLiquidado);
      if (!(valor > 0)) return F.toast('Informe o valor a liquidar.', 'warning');
      if (valor > salLiq) return F.toast(`O valor excede o saldo a liquidar (${F.money(salLiq)}).`, 'warning');
      if (!historico) return F.toast('O histórico da liquidação é obrigatório.', 'warning');
      ocupado = true;
      try {
        const body = {
          dataLiquidacao: f.get('dataLiquidacao') || F.todayStr(),
          documentoFiscal: (f.get('documentoFiscal') || '').toString() || null,
          historico,
          valor
        };
        const chave = F.chaveAcao('fin.liquidacao.criar', body);
        await F.postJson(`/api/financeiro/empenhos/${idEmpe}/liquidacoes`, body, chave);
        F.concluirAcao('fin.liquidacao.criar', body);
        F.toast('Liquidação registrada.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalLiquidar'))?.hide();
        ev.target.reset();
        carregar();
      } catch (err) { F.falha('Não foi possível registrar a liquidação', err); }
      finally { ocupado = false; }
    });

    // ---------------- anular empenho (parcial/integral, recálculo no servidor)
    document.getElementById('btn-anular').addEventListener('click', () => {
      if (ocupado || !atual) return;
      const vigente = atual.valorTotal - atual.valorAnulado;
      document.getElementById('hint-anular-empenho').textContent = `Líquido vigente: ${F.money(vigente)} · Anulado até agora: ${F.money(atual.valorAnulado)}`;
      document.getElementById('anu-valor').value = '';
      new bootstrap.Modal(document.getElementById('modalAnularEmpenho')).show();
    });

    $('#form-modal-anular-empenho').on('submit', async ev => {
      ev.preventDefault();
      const f = new FormData(ev.target);
      const valor = Number(f.get('valor'));
      const motivo = (f.get('motivo') || '').toString().trim();
      if (!atual) return;
      if (!(valor > 0)) return F.toast('Informe o valor da anulação.', 'warning');
      if (!motivo) return F.toast('O motivo da anulação é obrigatório.', 'warning');
      const vigente = atual.valorTotal - atual.valorAnulado;
      if (valor > vigente) return F.toast(`A anulação não pode exceder o líquido vigente (${F.money(vigente)}).`, 'warning');
      ocupado = true;
      try {
        const d = await F.getJson(`/api/financeiro/empenhos/${idEmpe}`); // estado vigente no servidor
        const novaAnulado = d.valorAnulado + valor;
        const statusApós = F.calc.statusEmpenho(d.valorTotal, novaAnulado, d.valorLiquidado, d.valorPago);
        F.confirmarReversao({
          titulo: `Anular empenho ${d.numero} por ${F.money(valor)}`,
          linhas: [
            { rotulo: 'Valor total do empenho', valor: F.money(d.valorTotal) },
            { rotulo: 'Anulado até agora', valor: F.money(d.valorAnulado) },
            { rotulo: 'Anulação solicitada', valor: F.money(valor) },
            { rotulo: 'Anulado após', valor: F.money(novaAnulado) },
            { rotulo: 'Saldo a liquidar após', valor: F.money(F.calc.saldoALiquidar(d.valorTotal, novaAnulado, d.valorLiquidado)) },
            { rotulo: 'Status após', valor: statusApós }
          ],
          pergunta: statusApós === 'ANULADO'
            ? 'O empenho será ANULADO integralmente: não poderá mais ser liquidado nem pago.'
            : 'Anulação parcial: apenas o saldo restante segue disponível para liquidação e pagamento.',
          onConfirm: async () => {
            try {
              const body = { valor, motivo };
              const chave = F.chaveAcao('fin.empenho.anular:' + idEmpe, body);
              await F.postJson(`/api/financeiro/empenhos/${idEmpe}/anular`, body, chave);
              F.concluirAcao('fin.empenho.anular:' + idEmpe, body);
              F.toast('Anulação registrada.', 'success');
              bootstrap.Modal.getInstance(document.getElementById('modalAnularEmpenho'))?.hide();
              ev.target.reset();
              carregar();
              return true;
            } catch (err) { F.falha('Não foi possível anular o empenho', err); return false; }
          }
        });
      } catch (err) { F.falha('Não foi possível preparar a anulação do empenho', err); ocupado = false; }
    });

    // ---------------- pagar liquidação
    $('#form-modal-pagar').on('submit', async ev => {
      ev.preventDefault();
      const f = new FormData(ev.target);
      const liqId = Number(f.get('liquidacaoId'));
      const valor = Number(f.get('valor'));
      const historico = (f.get('historico') || '').toString().trim();
      if (!liqId) return;
      const max = Number(document.getElementById('pag-valor').getAttribute('max') || 0);
      if (!(valor > 0)) return F.toast('Informe o valor do pagamento.', 'warning');
      if (max > 0 && valor > max) return F.toast(`O valor excede o saldo a pagar (${F.money(max)}).`, 'warning');
      if (!historico) return F.toast('O histórico do pagamento é obrigatório.', 'warning');
      ocupado = true;
      try {
        const body = {
          dataPagamento: f.get('dataPagamento') || F.todayStr(),
          formaPagamento: f.get('formaPagamento'),
          contaBancaria: (f.get('contaBancaria') || '').toString() || null,
          historico,
          valor
        };
        const chave = F.chaveAcao('fin.pagamento.criar', body);
        await F.postJson(`/api/financeiro/liquidacoes/${liqId}/pagamentos`, body, chave);
        F.concluirAcao('fin.pagamento.criar', body);
        F.toast('Pagamento efetuado.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalPagar'))?.hide();
        ev.target.reset();
        carregar();
      } catch (err) { F.falha('Não foi possível efetuar o pagamento', err); }
      finally { ocupado = false; }
    });

    (async function init() {
      await F.carregarCatalogos();
      carregar();
    })();
  }

  if (tela === 'lista') iniciarLista();
  else if (tela === 'criar') iniciarCriar();
  else if (tela === 'detalhe') iniciarDetalhe();
})();
