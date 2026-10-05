// SIGOV PLUS · Financeiro (SIAFIC) — helpers compartilhados pelas telas operacionais.
// Convenções: dinheiro em BRL com precisão monetária; datas ISO (yyyy-MM-dd) na API;
// todo erro exibe correlation ID quando disponível; seletores buscam itens por NOME.
window.sigovFin = (() => {
  const api = () => window.sigovApi;

  function unwrap(body) {
    if (body && body.success === false) {
      const err = new Error(body.message || 'Falha na operação.');
      err.correlationId = body.correlationId || null;
      throw err;
    }
    return body ? body.data : null;
  }

  async function getJson(path) { return unwrap(await api().request(path)); }
  async function postJson(path, payload, idempotencyKey) { return unwrap(await api().request(path, { method: 'POST', body: JSON.stringify(payload == null ? {} : payload), ...(idempotencyKey ? { headers: { 'Idempotency-Key': idempotencyKey } } : {}) })); }

  // ------------ idempotência: chave estável por ação+carga; a mesma chave é reenviada em nova
  // tentativa do usuário e descartada somente após sucesso (o servidor decide replay/conflito).
  const _idemSlots = {};
  function _hashSlot(s) { let h = 5381; for (let i = 0; i < s.length; i++) h = ((h << 5) + h + s.charCodeAt(i)) | 0; return (h >>> 0).toString(36); }
  function novaChaveIdempotencia() { return (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : 'k-' + Date.now().toString(36) + '-' + Math.random().toString(36).slice(2, 10); }
  function chaveAcao(nomeAcao, payload) { const slot = _hashSlot(nomeAcao + '|' + JSON.stringify(payload == null ? {} : payload)); if (!_idemSlots[slot]) _idemSlots[slot] = novaChaveIdempotencia(); return _idemSlots[slot]; }
  function concluirAcao(nomeAcao, payload) { delete _idemSlots[_hashSlot(nomeAcao + '|' + JSON.stringify(payload == null ? {} : payload))]; }

  // ---------------------------------------------------------------- feedback
  function toast(msg, type) {
    const el = document.getElementById('sigov-alerts');
    if (!el) return;
    el.innerHTML = `<div class="alert alert-${type || 'info'} alert-dismissible fade show" role="alert">${msg}` +
      `<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Fechar"></button></div>`;
  }

  // Erro seguro: mensagem classificada por status + correlation ID curto para suporte.
  // Mensagens técnicas do servidor (sem stack trace/SQL) são preservadas como texto.
  function falha(contexto, err) {
    const s = err && err.status;
    const classe = s === 401 ? 'Sessão expirada' : s === 403 ? 'Permissão insuficiente' : s === 404 ? 'Recurso não encontrado' : s === 409 ? 'Conflito' : s >= 500 ? 'Falha no servidor' : null;
    const corr = err && err.correlationId ? ` · Correlation: ${escapa(String(err.correlationId).slice(0, 8).toUpperCase())}` : '';
    const msg = err && err.message ? escapa(err.message) : 'falha inesperada.';
    toast(`${contexto}${classe ? ' (' + classe.toLowerCase() + ')' : ''}: ${msg}${corr}`, 'danger');
  }

  // ---------------------------------------------------------------- formatos
  function money(v) {
    const n = Number(v);
    return (Number.isFinite(n) ? n : 0).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  // "1.234,56" | "1234.56" | número → decimal
  function parseMoney(s) {
    if (typeof s === 'number') return s;
    let t = String(s == null ? '' : s).trim().replace(/^R\$\s*/i, '');
    if (!t) return NaN;
    if (t.includes(',')) t = t.replace(/\./g, '').replace(',', '.');
    const n = parseFloat(t);
    return Number.isFinite(n) ? n : NaN;
  }

  function dateStr(v) {
    if (!v) return '—';
    return String(v).slice(0, 10).split('-').reverse().join('/');
  }

  function todayStr() { return new Date().toISOString().slice(0, 10); }

  const STATUS_MAP = {
    EMITIDO: ['info', 'Emitido'],
    LIQUIDADO_PARCIAL: ['primary', 'Liquidado parcial'],
    LIQUIDADO_TOTAL: ['success', 'Liquidado'],
    PAGO_PARCIAL: ['warning', 'Pago parcial'],
    PAGO_TOTAL: ['dark', 'Pago'],
    ANULADO: ['secondary', 'Anulado'],
    LANCADA: ['info', 'Lançada'],
    PARCIALMENTE_ARRECADADA: ['warning', 'Arrecadação parcial'],
    ARRECADADA: ['success', 'Arrecadada'],
    CANCELADA: ['secondary', 'Cancelada'],
    CANCELADO: ['secondary', 'Cancelado'],
    LIQUIDADA: ['success', 'Liquidada'],
    ANULADA: ['secondary', 'Anulada'],
    EFETUADO: ['success', 'Efetuado']
  };

  function statusBadge(s) {
    const m = STATUS_MAP[s] || ['light', s || '—'];
    return `<span class="badge bg-${m[0]}">${m[1]}</span>`;
  }

  // --------------------------------------------- fórmulas centrais (mesmas do backend)
  const calc = {
    saldoALiquidar: (total, anulado, liquidado) => total - anulado - liquidado,
    saldoAPagar: (liquidado, pago) => liquidado - pago,
    saldoArrecadar: (valor, arrecadado) => valor - arrecadado,
    statusEmpenho: (valorTotal, valorAnulado, valorLiquidado, valorPago) => {
      if (valorAnulado >= valorTotal) return 'ANULADO';
      if (valorPago > 0 && valorPago >= valorLiquidado) return 'PAGO_TOTAL';
      if (valorPago > 0 && valorPago < valorLiquidado) return 'PAGO_PARCIAL';
      const efetivo = valorTotal - valorAnulado;
      if (valorLiquidado === efetivo) return 'LIQUIDADO_TOTAL';
      if (valorLiquidado > 0) return 'LIQUIDADO_PARCIAL';
      return 'EMITIDO';
    },
    statusLancamento: (valor, arrecadado) => {
      if (arrecadado >= valor && valor > 0) return 'ARRECADADA';
      if (arrecadado > 0) return 'PARCIALMENTE_ARRECADADA';
      return 'LANCADA';
    }
  };

  // ------------------- seletor pesquisável por NOME (busca via API + escolha por nome)
  // $input: jQuery do input de texto. config.fetch(termo) → Promise<Array<{id,label}>>.
  function selectSearch($input, fetchItems, placeholder) {
    $input.attr('placeholder', placeholder || 'Digite para buscar por nome…');
    let list = $('<div class="list-group position-absolute w-100 shadow d-none" style="z-index:1060;max-height:240px;overflow:auto"></div>');
    $input.parent().addClass('position-relative').append(list);

    let timer = null;
    $input.off('input.fin focus.fin').on('input.fin', () => {
      $input.removeData('fin-ok');
      const termo = $input.val() || '';
      clearTimeout(timer);
      if (!termo.trim()) { list.addClass('d-none'); return; }
      timer = setTimeout(async () => {
        try {
          const itens = (await fetchItems(termo.trim())) || [];
          list.empty();
          if (!itens.length) list.append('<span class="list-group-item text-muted small">Nada encontrado</span>');
          itens.forEach(it => list.append(`<button type="button" class="list-group-item list-group-item-action py-2 fin-opt" data-id="${it.id}" data-label="${escapa(it.label)}"><strong>${escapa(it.label)}</strong>${it.extra ? ` <span class="text-muted small">· ${escapa(it.extra)}</span>` : ''}</button>`));
          list.removeClass('d-none');
        } catch { list.empty(); list.addClass('d-none'); }
      }, 250);
    }).on('focus.fin', () => { if ($input.val() && !list.children().length) $input.trigger('input.fin'); });

    $(document).off('click.fin-' + $input.attr('id')).on('click.fin-' + $input.attr('id'), e => {
      if (!$(e.target).closest(`#${$input.attr('id')}, .fin-opt`).length) list.addClass('d-none');
    });

    list.on('click', '.fin-opt', function () {
      $input.val($(this).attr('data-label') || '');
      $input.data('fin-id', $(this).data('id')).data('fin-ok', true);
      list.addClass('d-none');
    });

    return {
      ok: () => $input.data('fin-ok') === true,
      id: () => { const v = $input.data('fin-id'); return v == null || v === '' ? null : Number(v); }
    };
  }

  // ------------- confirmação prévia de reversão/alteração crítica (recalcula no servidor)
  let _confirmModal;
  function ensureConfirmModal() {
    if (_confirmModal) return _confirmModal;
    $('body').append(
      '<div class="modal fade" id="finModalConfirm" tabindex="-1">' +
      '<div class="modal-dialog modal-lg"><div class="modal-content">' +
      '<div class="modal-header"><h5 class="modal-title" id="finConfirmTitle"></h5><button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Fechar"></button></div>' +
      '<div class="modal-body"><div id="finConfirmRows"></div><div id="finConfirmCampos" class="mb-2"></div><p id="finConfirmQuestion" class="form-text mt-2"></p>' +
      '<input type="hidden" id="finConfirmCorrelation"></div>' +
      '<div class="modal-footer"><button type="button" class="btn btn-outline-secondary" data-bs-dismiss="modal">Voltar</button>' +
      '<button type="button" class="btn btn-warning" id="finConfirmOk">Confirmar</button></div>' +
      '</div></div></div>');
    _confirmModal = new bootstrap.Modal(document.getElementById('finModalConfirm'));
    return _confirmModal;
  }

  function confirmarReversao({ titulo, linhas, campos, pergunta, onConfirm }) {
    ensureConfirmModal();
    $('#finConfirmTitle').html(titulo);
    const rows = (linhas || []).map(l => `<tr><td>${escapa(l.rotulo)}</td><td class="text-end fw-semibold">${l.valor}</td></tr>`).join('');
    $('#finConfirmRows').html(rows ? `<table class="table table-sm align-middle mb-1">${rows}</table>` : '');
    $('#finConfirmCampos').html((campos || []).map(c => c.tipo === 'textarea'
      ? `<label class="form-label">${escapa(c.label)}</label><textarea id="finCampo_${c.id}" class="form-control mb-2" rows="2"></textarea>`
      : `<label class="form-label">${escapa(c.label)}</label><input type="${c.tipo || 'text'}" id="finCampo_${c.id}" class="form-control mb-2">`).join(''));
    $('#finConfirmQuestion').html(pergunta || 'Esta ação recalcula saldos e status no servidor e registra auditoria com motivo. Deseja confirmar?');
    $('#finConfirmOk').prop('disabled', false).off('click.fin').on('click.fin', async function () {
      this.disabled = true;
      const valores = {};
      let completo = true;
      (campos || []).forEach(c => {
        const v = ($('#finCampo_' + c.id).val() || '').toString().trim();
        valores[c.id] = v;
        if (c.obrigatorio && !v) completo = false;
      });
      if (!completo) { toast('Preencha todos os campos obrigatórios para continuar.', 'warning'); this.disabled = false; return; }
      try {
        // onConfirm resolve com true → sucesso: fecha a prévia. Resolve com false/undefined
        // → falha recuperável: mantém o modal aberto com os campos e a chave idempotente preservados.
        const ok = await onConfirm(valores);
        if (ok === true) _confirmModal.hide();
      } finally { this.disabled = false; }
    });
    _confirmModal.show();
  }

  // ---------------------------------------------------------------- exportação
  async function baixarArquivo(recurso, formato) {
    const buf = await api().request(`/api/financeiro/export/${recurso}.${formato}`);
    const blob = buf instanceof ArrayBuffer
      ? new Blob([buf], { type: formato === 'json' ? 'application/json' : 'text/csv' })
      : new Blob([JSON.stringify(buf, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = `${recurso}.${formato}`;
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(url), 2000);
  }

  // ------------------------------------- catálogos com nome (cache por sessão)
  async function carregarCatalogos() {
    const cache = window.__finCats || (window.__finCats = {});
    const planos = [
      ['programas', '/api/financeiro/programas?pageSize=200'],
      ['acoes', '/api/financeiro/acoes?pageSize=200'],
      ['naturezasDespesa', '/api/financeiro/naturezas-despesa?pageSize=200'],
      ['naturezasReceita', '/api/financeiro/naturezas-receita?pageSize=200'],
      ['fontesRecurso', '/api/financeiro/fontes-recurso?pageSize=200'],
      ['orcamentoDespesas', '/api/financeiro/orcamento/despesas?pageSize=200'],
      ['orcamentoReceitas', '/api/financeiro/orcamento/receitas?pageSize=200']
    ];
    await Promise.all(planos.map(async ([key, url]) => {
      if (cache[key]) return;
      try {
        const paged = await getJson(url);
        cache[key] = (paged && paged.items) || [];
        cache[key] = cache[key] || [];
      } catch { cache[key] = []; }
    }));
    return cache;
  }

  function nomePorId(cacheKey, id, suffixo = '#') {
    const lista = (window.__finCats && window.__finCats[cacheKey]) || [];
    const item = lista.find(i => i.id === id);
    return item ? item.nome : `${suffixo}${id}`;
  }

  function labelDotacao(item) {
    if (!item) return '';
    const a = nomePorId('acoes', item.acaoId);
    const n = nomePorId('naturezasDespesa', item.naturezaDespesaId);
    const f = nomePorId('fontesRecurso', item.fonteRecursoId);
    return `Dot ${item.id} · ${a} · ${n} · ${f} · saldo ${money(item.saldoDisponivel)}`;
  }

  // ------------------------------------------------ busca por nome (API reais)
  function qs(params) {
    const u = new URLSearchParams();
    Object.entries(params || {}).forEach(([k, v]) => { if (v !== null && v !== undefined && String(v) !== '') u.set(k, v); });
    const s = u.toString();
    return s ? '?' + s : '';
  }

  const CATALOGO_URLS = {
    programas: '/api/financeiro/programas',
    acoes: '/api/financeiro/acoes',
    naturezaDespesa: '/api/financeiro/naturezas-despesa',
    naturezaReceita: '/api/financeiro/naturezas-receita',
    fontesRecurso: '/api/financeiro/fontes-recurso'
  };

  function buscarCatalogo(chave, termo) {
    const base = CATALOGO_URLS[chave];
    if (!base) return Promise.resolve([]);
    return getJson(base + qs({ nome: termo, pageSize: 8 }))
      .then(p => (p.items || []).map(x => ({ id: x.id, label: x.nome, extra: x.codigo ? String(x.codigo) : undefined })))
      .catch(() => []);
  }

  function buscarPessoas(termo) {
    return getJson('/api/pessoas' + qs({ termo, pageSize: 8 }))
      .then(p => (p.items || []).map(x => ({ id: x.id, label: x.nome, extra: [x.tipoPessoa, x.documento].filter(Boolean).join(' · ') })))
      .catch(() => []);
  }

  function filtrarPorTermo(itens, termo) {
    const t = (termo || '').trim().toLowerCase();
    return t ? itens.filter(i => (i.label + ' ' + (i.extra || '') + ' ' + i.id).toLowerCase().includes(t)) : itens;
  }

  async function buscarDotacaoDespesa(termo) {
    let lista = window.__finCats && window.__finCats.orcamentoDespesas;
    if (!lista || !lista.length) {
      try { lista = (((await getJson('/api/financeiro/orcamento/despesas?pageSize=200')) || {}).items) || []; } catch { lista = []; }
    }
    return filtrarPorTermo(lista.map(i => ({
      id: i.id,
      label: `Dot ${i.id} · ${nomePorId('acoes', i.acaoId)} · ${nomePorId('naturezasDespesa', i.naturezaDespesaId)}`,
      extra: `fonte ${nomePorId('fontesRecurso', i.fonteRecursoId)} · saldo a empenhar ${money(i.saldoDisponivel)}`
    })), termo);
  }

  async function buscarDotacaoReceita(termo) {
    let lista = window.__finCats && window.__finCats.orcamentoReceitas;
    if (!lista || !lista.length) {
      try { lista = (((await getJson('/api/financeiro/orcamento/receitas?pageSize=200')) || {}).items) || []; } catch { lista = []; }
    }
    return filtrarPorTermo(lista.map(i => ({
      id: i.id,
      label: `Rec ${i.id} · ${nomePorId('naturezasReceita', i.naturezaReceitaId)}`,
      extra: `fonte ${nomePorId('fontesRecurso', i.fonteRecursoId)} · previsto ${money(i.previsaoAtualizada)}`
    })), termo);
  }

  function escapa(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  }

  return { unwrap, getJson, postJson, novaChaveIdempotencia, chaveAcao, concluirAcao, toast, falha, money, parseMoney, dateStr, todayStr, statusBadge, calc, selectSearch, confirmarReversao, baixarArquivo, carregarCatalogos, nomePorId, labelDotacao, escapa, qs, buscarCatalogo, buscarPessoas, buscarDotacaoDespesa, buscarDotacaoReceita };
})();
