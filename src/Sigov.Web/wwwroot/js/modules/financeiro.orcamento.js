// SIGOV PLUS · Financeiro SIAFIC — Orçamento do exercício: dotações (despesa) e previsão (receita).
// Fórmulas exibidas (mesmas do backend FinanceiroInvariantes):
//   Saldo a empenhar  = dotação vigente − empenhado (calculado no servidor)
//   Saldo a liquidar  = empenhado − liquidado
//   Saldo a pagar     = liquidado − pago
//   Saldo a arrecadar = previsão atualizada − arrecadado
(function () {
  const F = window.sigovFin;
  if (!F || !document.querySelector('[data-fin-tela="orcamento"]')) return;
  const SIZE = 50;
  let paginaDespesa = 1, paginaReceita = 1;
  const selects = {};

  function sel(id) { const s = selects[id]; return s ? s.id() : null; }
  function limparSel(...ids) { ids.forEach(id => $('#' + id).val('').removeData('fin-ok').removeData('fin-id')); }
  function moedaNeg(v) { return `<span class="${Number(v) < 0 ? 'text-danger fw-semibold' : ''}">${F.money(v)}</span>`; }

  function pagerHtml(prefix, pagina, totalItems) {
    const totalPag = Math.max(1, Math.ceil((totalItems || 0) / SIZE));
    return `<div class="d-flex align-items-center gap-2 mt-2">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${pagina <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${pagina} de ${totalPag} · ${totalItems || 0} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${pagina >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  async function init() {
    await F.carregarCatalogos();
    const mapaBusca = {
      'fin-fil-acao': 'acoes', 'fin-fil-nat': 'naturezaDespesa', 'fin-fil-fonte': 'fontesRecurso',
      'fin-fil-natrec': 'naturezaReceita', 'fin-fil-fontec': 'fontesRecurso',
      'fin-md-programa': 'programas', 'fin-md-acao': 'acoes', 'fin-md-nat': 'naturezaDespesa', 'fin-md-fonte': 'fontesRecurso',
      'fin-mdr-nat': 'naturezaReceita', 'fin-mdr-fonte': 'fontesRecurso'
    };
    Object.entries(mapaBusca).forEach(([id, chave]) => {
      const $i = $('#' + id);
      if ($i.length) selects[id] = F.selectSearch($i, termo => F.buscarCatalogo(chave, termo));
    });

    $('#btn-filtrar-despesas').on('click', () => { paginaDespesa = 1; carregarDespesas(); });
    $('#btn-limpar-despesas').on('click', () => { limparSel('fin-fil-acao', 'fin-fil-nat', 'fin-fil-fonte'); paginaDespesa = 1; carregarDespesas(); });
    $('#btn-filtrar-receitas').on('click', () => { paginaReceita = 1; carregarReceitas(); });
    $('#btn-limpar-receitas').on('click', () => { limparSel('fin-fil-natrec', 'fin-fil-fontec'); paginaReceita = 1; carregarReceitas(); });
    $('#pager-area-despesas').on('click', '.pager-prev', () => { if (paginaDespesa > 1) { paginaDespesa--; carregarDespesas(); } });
    $('#pager-area-despesas').on('click', '.pager-next', () => { paginaDespesa++; carregarDespesas(); });
    $('#pager-area-receitas').on('click', '.pager-prev', () => { if (paginaReceita > 1) { paginaReceita--; carregarReceitas(); } });
    $('#pager-area-receitas').on('click', '.pager-next', () => { paginaReceita++; carregarReceitas(); });

    $('#form-nova-dotacao').on('submit', async function (ev) {
      ev.preventDefault();
      const f = new FormData(this);
      const payload = {
        programaId: sel('fin-md-programa'), acaoId: sel('fin-md-acao'),
        naturezaDespesaId: sel('fin-md-nat'), fonteRecursoId: sel('fin-md-fonte'),
        dotacaoInicial: F.parseMoney(f.get('dotacaoInicial'))
      };
      if (!payload.programaId) return F.toast('Selecione o programa.', 'warning');
      if (!payload.acaoId) return F.toast('Selecione a ação.', 'warning');
      if (!payload.naturezaDespesaId) return F.toast('Selecione a natureza de despesa.', 'warning');
      if (!payload.fonteRecursoId) return F.toast('Selecione a fonte de recurso.', 'warning');
      if (!(payload.dotacaoInicial > 0)) return F.toast('Informe uma dotação inicial maior que zero.', 'warning');
      const btn = this.querySelector('button[type="submit"]'); btn.disabled = true;
      try {
        await F.postJson('/api/financeiro/orcamento/despesas', payload);
        F.toast('Dotação criada com sucesso.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalNovaDotacao'))?.hide();
        this.reset(); limparSel('fin-md-programa', 'fin-md-acao', 'fin-md-nat', 'fin-md-fonte');
        carregarDespesas();
      } catch (err) { F.falha('Não foi possível criar a dotação', err); }
      finally { btn.disabled = false; }
    });

    $('#form-movimentar').on('submit', async function (ev) {
      ev.preventDefault();
      const f = new FormData(this);
      const id = Number($('#fin-mov-id').val());
      const payload = { tipoMovimentacao: f.get('tipoMovimentacao'), valor: F.parseMoney(f.get('valor')), historico: (f.get('historico') || '').toString().trim() };
      if (!(payload.valor > 0)) return F.toast('Informe o valor da movimentação.', 'warning');
      if (!payload.historico) return F.toast('O histórico da movimentação é obrigatório.', 'warning');
      const btn = this.querySelector('button[type="submit"]'); btn.disabled = true;
      try {
        await F.postJson(`/api/financeiro/orcamento/despesas/${id}/movimentar`, payload);
        F.toast('Movimentação orçamentária registrada.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalMovimentar'))?.hide();
        this.reset();
        carregarDespesas();
      } catch (err) { F.falha('Não foi possível registrar a movimentação', err); }
      finally { btn.disabled = false; }
    });

    document.addEventListener('click', ev => {
      const b = ev.target.closest('.btn-movimentar');
      if (!b) return;
      $('#fin-mov-id').val(b.getAttribute('data-id'));
      $('#mov-resumo').text(`Dotação #${b.getAttribute('data-id')} · ${b.getAttribute('data-rotulo') || ''}`);
      new bootstrap.Modal(document.getElementById('modalMovimentar')).show();
    });

    $('#form-nova-receita').on('submit', async function (ev) {
      ev.preventDefault();
      const f = new FormData(this);
      const payload = { naturezaReceitaId: sel('fin-mdr-nat'), fonteRecursoId: sel('fin-mdr-fonte'), previsaoInicial: F.parseMoney(f.get('previsaoInicial')) };
      if (!payload.naturezaReceitaId) return F.toast('Selecione a natureza de receita.', 'warning');
      if (!payload.fonteRecursoId) return F.toast('Selecione a fonte de recurso.', 'warning');
      if (!(payload.previsaoInicial > 0)) return F.toast('Informe a previsão inicial maior que zero.', 'warning');
      const btn = this.querySelector('button[type="submit"]'); btn.disabled = true;
      try {
        await F.postJson('/api/financeiro/orcamento/receitas', payload);
        F.toast('Previsão de receita criada.', 'success');
        bootstrap.Modal.getInstance(document.getElementById('modalNovaReceita'))?.hide();
        this.reset(); limparSel('fin-mdr-nat', 'fin-mdr-fonte');
        carregarReceitas();
      } catch (err) { F.falha('Não foi possível criar a previsão de receita', err); }
      finally { btn.disabled = false; }
    });

    carregarDespesas();
    carregarReceitas();
  }

  async function carregarDespesas() {
    const tb = $('#tbody-despesas');
    tb.html('<tr><td colspan="12" class="text-muted py-3">Carregando…</td></tr>');
    try {
      const paged = await F.getJson('/api/financeiro/orcamento/despesas' + F.qs({
        page: paginaDespesa, pageSize: SIZE,
        acaoId: sel('fin-fil-acao'), naturezaDespesaId: sel('fin-fil-nat'), fonteRecursoId: sel('fin-fil-fonte')
      }));
      const itens = paged.items || [];
      tb.html(itens.length ? itens.map(i => {
        const vigente = i.dotacaoInicial + i.suplementacoes - i.reducoes;
        const bruto = `Inicial ${F.money(i.dotacaoInicial)} · Suplem. +${F.money(i.suplementacoes)} · Red. −${F.money(i.reducoes)} · Reservado ${F.money(i.reservado)}`;
        return `<tr>
          <td>${i.id}</td>
          <td>${F.escapa(F.nomePorId('programas', i.programaId))}<br><span class="small text-muted">${F.escapa(F.nomePorId('acoes', i.acaoId))}</span></td>
          <td>${F.escapa(F.nomePorId('naturezasDespesa', i.naturezaDespesaId))}</td>
          <td>${F.escapa(F.nomePorId('fontesRecurso', i.fonteRecursoId))}</td>
          <td class="text-end" title="${F.escapa(bruto)}">${F.money(vigente)}</td>
          <td class="text-end">${F.money(i.empenhado)}</td>
          <td class="text-end">${F.money(i.liquidado)}</td>
          <td class="text-end">${F.money(i.pago)}</td>
          <td class="text-end">${moedaNeg(i.saldoDisponivel)}</td>
          <td class="text-end">${moedaNeg(i.empenhado - i.liquidado)}</td>
          <td class="text-end">${moedaNeg(i.liquidado - i.pago)}</td>
          <td class="text-nowrap"><button type="button" class="btn btn-sm btn-outline-primary btn-movimentar" data-id="${i.id}" data-rotulo="${F.escapa(F.nomePorId('acoes', i.acaoId))}">Movimentar</button></td>
        </tr>`;
      }).join('') : '<tr><td colspan="12" class="text-muted py-3">Nenhuma dotação para os filtros atuais.</td></tr>');
      $('#pager-area-despesas').html(pagerHtml('despesas', paginaDespesa, paged.totalItems));
    } catch (err) { F.falha('Falha ao carregar as dotações', err); }
  }

  async function carregarReceitas() {
    const tb = $('#tbody-receitas');
    tb.html('<tr><td colspan="9" class="text-muted py-3">Carregando…</td></tr>');
    try {
      const paged = await F.getJson('/api/financeiro/orcamento/receitas' + F.qs({
        page: paginaReceita, pageSize: SIZE,
        naturezaReceitaId: sel('fin-fil-natrec'), fonteRecursoId: sel('fin-fil-fontec')
      }));
      const itens = paged.items || [];
      tb.html(itens.length ? itens.map(i => `<tr>
        <td>${i.id}</td>
        <td>${F.escapa(F.nomePorId('naturezasReceita', i.naturezaReceitaId))}</td>
        <td>${F.escapa(F.nomePorId('fontesRecurso', i.fonteRecursoId))}</td>
        <td class="text-end">${F.money(i.previsaoInicial)}</td>
        <td class="text-end">${F.money(i.previsaoAtualizada)}</td>
        <td class="text-end">${F.money(i.lancado)}</td>
        <td class="text-end">${F.money(i.arrecadado)}</td>
        <td class="text-end">${moedaNeg(i.previsaoAtualizada - i.arrecadado)}</td>
        <td class="small ${i.ativo ? 'text-success' : 'text-secondary'}">${i.ativo ? 'Ativa' : 'Inativa'}</td>
      </tr>`).join('') : '<tr><td colspan="9" class="text-muted py-3">Nenhuma previsão de receita para os filtros atuais.</td></tr>');
      $('#pager-area-receitas').html(pagerHtml('receitas', paginaReceita, paged.totalItems));
    } catch (err) { F.falha('Falha ao carregar a previsão de receita', err); }
  }

  init();
})();
