// SIGOV PLUS · Financeiro SIAFIC — Receitas: lançamentos, arrecadação parcial até o total,
// cancelamento de lançamento e estorno de arrecadação com prévia de recálculo.
// Servidor é autoridade: estado vigente é reconsultado antes de cada reversão.
(function () {
  const F = window.sigovFin;
  if (!F || !document.querySelector('[data-fin-tela="receitas"]')) return;
  const SIZE = 50;
  let pagina = 1, ocupado = false;
  let selDotacao = null, selContribuinteNovo = null, selFilContribuinte = null;
  let lancamentoAberto = null; // id do lançamento aberto no modal de detalhe

  function abrirModal(id) { const m = document.getElementById(id); if (m) new bootstrap.Modal(m).show(); }
  function limparSel(...ids) { ids.forEach(s => $(s).val('').removeData('fin-ok').removeData('fin-id')); }

  function pagerHtml(p, totalItems) {
    const totalPag = Math.max(1, Math.ceil((totalItems || 0) / SIZE));
    return `<div class="d-flex align-items-center gap-2 mt-2">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${p <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${p} de ${totalPag} · ${totalItems || 0} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${p >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  async function carregar() {
    const tb = $('#tbody-lancamentos');
    tb.html('<tr><td colspan="10" class="text-muted py-3">Carregando…</td></tr>');
    try {
      const paged = await F.getJson('/api/financeiro/receitas/lancamentos' + F.qs({
        page: pagina, pageSize: SIZE,
        numero: $('#rec-numero').val() || null,
        status: $('#rec-status').val() || null,
        contribuintePessoaId: selFilContribuinte ? selFilContribuinte.id() : null
      }));
      const itens = paged.items || [];
      tb.html(itens.length ? itens.map(l => {
        const saldo = F.calc.saldoArrecadar(l.valor, l.arrecadado);
        const acoes = [];
        if (l.status !== 'CANCELADA' && saldo > 0) acoes.push(`<button type="button" class="btn btn-sm btn-primary btn-arrecadar" data-id="${l.id}" data-saldo="${saldo}">Arrecadar</button>`);
        acoes.push(`<button type="button" class="btn btn-sm btn-outline-secondary btn-detalhe-lanc" data-id="${l.id}">Detalhes</button>`);
        if (l.status !== 'CANCELADA') acoes.push(`<button type="button" class="btn btn-sm btn-outline-danger btn-cancelar-lanc" data-id="${l.id}">Cancelar</button>`);
        return `<tr>
          <td>${F.escapa(l.numero)}</td>
          <td>${F.dateStr(l.dataLancamento)}</td>
          <td class="small">Prev. #${l.orcamentoReceitaId}</td>
          <td>${F.escapa(l.contribuinte || '—')}</td>
          <td>${F.escapa(l.historico || '—')}</td>
          <td class="text-end">${F.money(l.valor)}</td>
          <td class="text-end">${F.money(l.arrecadado)}</td>
          <td class="text-end ${saldo < 0 ? 'text-danger fw-semibold' : ''}">${F.money(saldo)}</td>
          <td>${F.statusBadge(l.status)}</td>
          <td class="text-nowrap">${acoes.join(' ')}</td>
        </tr>`;
      }).join('') : '<tr><td colspan="10" class="text-muted py-3">Nenhum lançamento de receita para os filtros atuais.</td></tr>');
      $('#pager-lancamentos').html(pagerHtml(pagina, paged.totalItems));
    } catch (err) { F.falha('Falha ao carregar os lançamentos de receita', err); }
  }

  async function abrirDetalhe(lancId) {
    try {
      const [l, arrs] = await Promise.all([
        F.getJson(`/api/financeiro/receitas/lancamentos/${lancId}`),
        F.getJson(`/api/financeiro/receitas/lancamentos/${lancId}/arrecadacoes`)
      ]);
      lancamentoAberto = lancId;
      document.getElementById('ldet-resumo').innerHTML = `
        <dl class="row g-2 small mb-2">
          <div class="col-sm-6"><dt class="text-muted">Lançamento</dt><dd class="mb-0 fw-semibold">${F.escapa(l.numero)} ${F.statusBadge(l.status)}</dd></div>
          <div class="col-sm-6"><dt class="text-muted">Data</dt><dd class="mb-0">${F.dateStr(l.dataLancamento)}</dd></div>
          <div class="col-sm-6"><dt class="text-muted">Contribuinte</dt><dd class="mb-0">${F.escapa(l.contribuinte || '—')}</dd></div>
          <div class="col-sm-6"><dt class="text-muted">Valor · Arrecadado · Saldo a arrecadar</dt><dd class="mb-0">${F.money(l.valor)} · ${F.money(l.arrecadado)} · ${F.money(F.calc.saldoArrecadar(l.valor, l.arrecadado))}</dd></div>
        </dl>`;
      document.getElementById('tbody-arrecadacoes').innerHTML = (arrs || []).map(a => `<tr>
        <td>${F.escapa(a.numero)}</td>
        <td>${F.dateStr(a.dataArrecadacao)}</td>
        <td>${F.escapa(a.formaArrecadacao)}</td>
        <td>${F.escapa(a.historico || '—')}</td>
        <td class="text-end">${F.money(a.valor)}</td>
        <td>${F.statusBadge(a.status)}</td>
        <td class="text-nowrap">${a.status === 'ARRECADADA' ? `<button type="button" class="btn btn-sm btn-outline-danger btn-cancelar-arq" data-arr-id="${a.id}" data-lanc-id="${l.id}" data-valor="${a.valor}">Estornar</button>` : '<span class="text-muted small">—</span>'}</td>
      </tr>`).join('') || '<tr><td colspan="7" class="text-muted py-3">Nenhuma arrecadação registrada.</td></tr>';
      abrirModal('modalDetalheLancamento');
    } catch (err) { F.falha('Não foi possível abrir o detalhe do lançamento', err); }
  }

  function iniciarArrecadar(btn) {
    const saldo = Number(btn.getAttribute('data-saldo'));
    document.getElementById('arr-lanc-id').value = btn.getAttribute('data-id');
    document.getElementById('hint-saldo-arrecadar').textContent = `Saldo a arrecadar deste lançamento: ${F.money(saldo)}`;
    const v = document.getElementById('arr-valor');
    v.value = '';
    v.setAttribute('max', String(saldo));
    document.getElementById('arr-data').value = F.todayStr();
    abrirModal('modalArrecadar');
  }

  async function cancelarLancamento(btn) {
    if (ocupado) return;
    ocupado = true;
    try {
      const id = Number(btn.getAttribute('data-id'));
      const l = await F.getJson(`/api/financeiro/receitas/lancamentos/${id}`);
      if (l.status === 'CANCELADA') { F.toast('Este lançamento já está cancelado.', 'warning'); return; }
      F.confirmarReversao({
        titulo: `Cancelar lançamento ${F.escapa(l.numero)}`,
        linhas: [
          { rotulo: 'Valor do lançamento', valor: F.money(l.valor) },
          { rotulo: 'Já arrecadado', valor: F.money(l.arrecadado) },
          { rotulo: 'Situação após', valor: 'CANCELADA (bloqueia novas arrecadações)' }
        ],
        campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo do cancelamento (obrigatório)', obrigatorio: true }],
        pergunta: 'O cancelamento zera o saldo a arrecadar, preserva o histórico de arrecadações já realizadas para auditoria e recomputa os acumuladores no servidor.',
        onConfirm: async v => {
          try {
            const body = { motivo: v.motivo };
            const chave = F.chaveAcao('fin.receita.lancamento.cancelar:' + id, body);
            await F.postJson(`/api/financeiro/receitas/lancamentos/${id}/cancelar`, body, chave);
            F.concluirAcao('fin.receita.lancamento.cancelar:' + id, body);
            F.toast('Lançamento cancelado.', 'success');
            carregar();
            if (lancamentoAberto === id) abrirDetalhe(id);
          } catch (err) { F.falha('Não foi possível cancelar o lançamento', err); }
        }
      });
    } catch (err) { F.falha('Não foi possível preparar o cancelamento do lançamento', err); }
    finally { ocupado = false; }
  }

  async function cancelarArrecadacao(btn) {
    if (ocupado) return;
    ocupado = true;
    try {
      const arrId = Number(btn.getAttribute('data-arr-id'));
      const lancId = Number(btn.getAttribute('data-lanc-id'));
      const valor = Number(btn.getAttribute('data-valor'));
      const l = await F.getJson(`/api/financeiro/receitas/lancamentos/${lancId}`);
      const apos = Math.max(0, l.arrecadado - valor);
      const statusApos = l.status === 'CANCELADA' ? 'CANCELADA' : F.calc.statusLancamento(l.valor, apos);
      F.confirmarReversao({
        titulo: `Estornar arrecadação (${F.money(valor)})`,
        linhas: [
          { rotulo: 'Valor do estorno', valor: F.money(valor) },
          { rotulo: 'Arrecadado após', valor: F.money(apos) },
          { rotulo: 'Status do lançamento após', valor: statusApos }
        ],
        campos: [{ id: 'motivo', tipo: 'textarea', label: 'Motivo do cancelamento (obrigatório)', obrigatorio: true }],
        pergunta: 'O estorno reduz o acumulado do lançamento e vincula a reversão à origem na auditoria com data, usuário e correlation ID.',
        onConfirm: async v => {
          try {
            const body = { motivo: v.motivo };
            const chave = F.chaveAcao('fin.receita.arrecadacao.cancelar:' + arrId, body);
            await F.postJson(`/api/financeiro/receitas/arrecadacoes/${arrId}/cancelar`, body, chave);
            F.concluirAcao('fin.receita.arrecadacao.cancelar:' + arrId, body);
            F.toast('Arrecadação cancelada (estorno registrado).', 'success');
            carregar();
            abrirDetalhe(lancId);
          } catch (err) { F.falha('Não foi possível cancelar a arrecadação', err); }
        }
      });
    } catch (err) { F.falha('Não foi possível preparar o cancelamento da arrecadação', err); }
    finally { ocupado = false; }
  }

  (async function init() {
    await F.carregarCatalogos();
    selDotacao = F.selectSearch($('#novo-dotacao'), termo => F.buscarDotacaoReceita(termo));
    selContribuinteNovo = F.selectSearch($('#novo-contribuinte'), termo => F.buscarPessoas(termo), 'Contribuinte (opcional — busque por nome)');
    selFilContribuinte = F.selectSearch($('#rec-contribuinte'), termo => F.buscarPessoas(termo));

    document.getElementById('btn-novo-lancamento').addEventListener('click', () => {
      document.getElementById('novo-data').value = F.todayStr();
      abrirModal('modalNovoLancamento');
    });

    $('#btn-rec-filtrar').on('click', () => { pagina = 1; carregar(); });
    $('#btn-rec-limpar').on('click', () => {
      $('#rec-numero').val(''); $('#rec-status').val('');
      limparSel('#rec-contribuinte');
      pagina = 1; carregar();
    });
    $('#pager-lancamentos').on('click', '.pager-prev', () => { if (pagina > 1) { pagina--; carregar(); } });
    $('#pager-lancamentos').on('click', '.pager-next', () => { pagina++; carregar(); });
    $('#tbody-lancamentos').on('click', '.btn-arrecadar', function () { iniciarArrecadar(this); });
    $('#tbody-lancamentos').on('click', '.btn-detalhe-lanc', function () { abrirDetalhe(Number(this.getAttribute('data-id'))); });
    $('#tbody-lancamentos').on('click', '.btn-cancelar-lanc', function () { cancelarLancamento(this); });
    $('#tbody-arrecadacoes').on('click', '.btn-cancelar-arq', function () { cancelarArrecadacao(this); });

    $('#form-novo-lancamento').on('submit', async function (ev) {
      ev.preventDefault();
      const f = new FormData(this);
      const payload = {
        orcamentoReceitaId: selDotacao ? selDotacao.id() : null,
        dataLancamento: f.get('dataLancamento') || F.todayStr(),
        contribuintePessoaId: selContribuinteNovo ? selContribuinteNovo.id() : null,
        historico: (f.get('historico') || '').toString().trim(),
        valor: F.parseMoney(f.get('valor'))
      };
      if (!payload.orcamentoReceitaId) return F.toast('Selecione a previsão de receita (busque por nome).', 'warning');
      if (!payload.historico) return F.toast('O histórico do lançamento é obrigatório.', 'warning');
      if (!(payload.valor > 0)) return F.toast('Informe o valor do lançamento.', 'warning');
      const btn = this.querySelector('button[type="submit"]');
      btn.disabled = true;
      try {
        const chave = F.chaveAcao('fin.receita.lancamento.criar', payload);
        await F.postJson('/api/financeiro/receitas/lancamentos', payload, chave);
        F.concluirAcao('fin.receita.lancamento.criar', payload);
        F.toast('Lançamento de receita registrado.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalNovoLancamento'))?.hide();
        this.reset();
        limparSel('#novo-dotacao', '#novo-contribuinte');
        carregar();
      } catch (err) { F.falha('Não foi possível registrar o lançamento', err); }
      finally { btn.disabled = false; }
    });

    $('#form-arrecadar').on('submit', async function (ev) {
      ev.preventDefault();
      const f = new FormData(this);
      const lancId = Number(document.getElementById('arr-lanc-id').value);
      const valor = F.parseMoney(f.get('valor'));
      const max = Number(document.getElementById('arr-valor').getAttribute('max') || 0);
      if (!lancId) return F.toast('Selecione o lançamento antes de arrecadar.', 'warning');
      if (!(valor > 0)) return F.toast('Informe o valor arrecadado.', 'warning');
      if (max > 0 && valor > max) return F.toast(`O valor excede o saldo a arrecadar (${F.money(max)}).`, 'warning');
      const historico = (f.get('historico') || '').toString().trim();
      if (!historico) return F.toast('O histórico da arrecadação é obrigatório.', 'warning');
      const btn = this.querySelector('button[type="submit"]');
      btn.disabled = true;
      try {
        const body = {
          dataArrecadacao: f.get('dataArrecadacao') || F.todayStr(),
          formaArrecadacao: f.get('formaArrecadacao'),
          valor,
          historico
        };
        const chave = F.chaveAcao('fin.receita.arrecadacao.criar', body);
        await F.postJson(`/api/financeiro/receitas/lancamentos/${lancId}/arrecadacoes`, body, chave);
        F.concluirAcao('fin.receita.arrecadacao.criar', body);
        F.toast('Arrecadação registrada.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalArrecadar'))?.hide();
        this.reset();
        carregar();
        if (lancamentoAberto) abrirDetalhe(lancamentoAberto);
      } catch (err) { F.falha('Não foi possível registrar a arrecadação', err); }
      finally { btn.disabled = false; }
    });

    carregar();
  })();
})();
