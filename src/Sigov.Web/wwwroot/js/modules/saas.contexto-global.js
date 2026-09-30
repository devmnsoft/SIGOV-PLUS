(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const state = { tenant: null, unit: null, exercises: [], version: null, requiresReason: false };
  let timer;
  let searchSeq = 0;
  let selectSeq = 0;

  const api = async (url, options = {}) => {
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', ...options.headers },
      ...options
    });
    if (!response.ok) {
      const err = await response.json().catch(() => ({}));
      throw new Error(err.message || 'Não foi possível concluir a operação.');
    }
    if (response.status === 204) return null;
    const body = await response.json();
    return body.data;
  };

  const error = message => {
    const el = $('ctxGeneralError');
    if (el) {
      el.textContent = message;
      el.classList.remove('d-none');
    }
    const retry = $('ctxRetry');
    if (retry) retry.classList.remove('d-none');
  };

  const clearError = () => {
    const el = $('ctxGeneralError');
    if (el) el.classList.add('d-none');
    const retry = $('ctxRetry');
    if (retry) retry.classList.add('d-none');
  };

  const fill = (element, rows, placeholder) => {
    if (!element) return;
    element.innerHTML = `<option value="">${placeholder}</option>`;
    rows.forEach(x => {
      const opt = new Option(`${x.nome}${x.situacao ? ` — ${x.situacao}` : ''}`, x.id);
      if (x.parentId !== undefined && x.parentId !== null) {
        opt.dataset.parent = x.parentId;
      }
      element.add(opt);
    });
    element.disabled = rows.length === 0;
  };

  const resetDependents = () => {
    state.unit = null;
    state.exercises = [];
    fill($('ctxUnit'), [], 'Carregando unidades...');
    $('ctxUnit').disabled = true;
    fill($('ctxExercise'), [], 'Aguardando unidade');
    $('ctxExercise').disabled = true;
    fill($('ctxSystem'), [], 'Carregando sistemas...');
    $('ctxSystem').disabled = true;
  };

  async function current() {
    try {
      const c = await api('/api/saas/contexto/atual');
      state.version = c?.versao || null;
      const summary = $('ctxSummary');
      if (summary) {
        summary.textContent = !c || c.isGlobal
          ? 'Contexto global — selecione uma organização institucional para iniciar.'
          : `${c.empresaNome || c.organizacaoNome || 'Organização'} • ${c.unidadeNome || 'Unidade'} • ${c.exercicioNome || 'Exercício'} • ${c.sistemaNome || 'Sistema'} • ${c.modoAcesso || 'Operacional'}`;
      }
      const banner = $('ctxAssistedBanner');
      if (banner) banner.classList.toggle('d-none', !c || c.isGlobal);
      const retBtn = $('ctxReturnGlobal');
      if (retBtn) retBtn.classList.toggle('d-none', !c || c.isGlobal);
    } catch (e) {
      error(e.message);
    }
  }

  async function search(term) {
    const currentSeq = ++searchSeq;
    try {
      const rows = await api(`/api/saas/contexto/empresas?busca=${encodeURIComponent(term)}&tamanho=15`);
      if (currentSeq !== searchSeq) return; // ignora resposta desatualizada fora de ordem
      const box = $('ctxCompanyResults');
      if (!box) return;
      box.innerHTML = '';
      rows.forEach(x => {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'list-group-item list-group-item-action';
        button.textContent = `${x.nome} (${x.codigo || x.esferaGoverno || 'Institucional'})`;
        button.onclick = () => selectTenant(x);
        box.appendChild(button);
      });
      box.classList.toggle('d-none', rows.length === 0);
    } catch (e) {
      if (currentSeq === searchSeq) error(e.message);
    }
  }

  async function selectTenant(tenant) {
    const currentSeq = ++selectSeq;
    state.tenant = tenant;
    const searchInput = $('ctxCompanySearch');
    if (searchInput) searchInput.value = tenant.nome;
    const box = $('ctxCompanyResults');
    if (box) box.classList.add('d-none');
    clearError();
    resetDependents();

    try {
      const [units, exercises, systems] = await Promise.all([
        api(`/api/saas/contexto/empresas/${tenant.id}/unidades`),
        api(`/api/saas/contexto/empresas/${tenant.id}/exercicios`),
        api(`/api/saas/contexto/empresas/${tenant.id}/sistemas`)
      ]);
      if (currentSeq !== selectSeq) return; // descarta se outra organização foi selecionada

      state.exercises = exercises;
      fill($('ctxUnit'), units, units.length ? 'Selecione a unidade' : 'Nenhuma unidade disponível');
      fill($('ctxSystem'), systems, systems.length ? 'Selecione o sistema' : 'Nenhum sistema disponível');
      fill($('ctxExercise'), [], 'Selecione a unidade primeiro');
    } catch (e) {
      if (currentSeq === selectSeq) error(e.message);
    }
  }

  const searchInput = $('ctxCompanySearch');
  if (searchInput) {
    searchInput.addEventListener('input', e => {
      state.tenant = null;
      resetDependents();
      clearTimeout(timer);
      const query = e.target.value.trim();
      if (query.length >= 2) {
        timer = setTimeout(() => search(query), 300);
      } else {
        const box = $('ctxCompanyResults');
        if (box) box.classList.add('d-none');
      }
    });
  }

  const unitSelect = $('ctxUnit');
  if (unitSelect) {
    unitSelect.addEventListener('change', e => {
      const option = e.target.value;
      state.unit = option ? Number(option) : null;
      const selectedOpt = e.target.selectedOptions[0];
      const entityId = selectedOpt && selectedOpt.dataset.parent ? Number(selectedOpt.dataset.parent) : 0;
      state.entityId = entityId;
      const relevantExercises = state.exercises.filter(x => !entityId || !x.parentId || x.parentId === entityId);
      fill($('ctxExercise'), relevantExercises, relevantExercises.length ? 'Selecione o exercício' : 'Nenhum exercício disponível');
    });
  }

  const reasonInput = $('ctxReason');
  if (reasonInput) {
    reasonInput.addEventListener('input', e => {
      const counter = $('ctxReasonCount');
      if (counter) counter.textContent = e.target.value.length;
    });
  }

  const form = $('ctxForm');
  if (form) {
    form.addEventListener('submit', async e => {
      e.preventDefault();
      clearError();
      if (!state.tenant || !$('ctxUnit').value || !$('ctxExercise').value || !$('ctxSystem').value) {
        return error('Preencha a organização, unidade, exercício e sistema.');
      }
      const unitOption = $('ctxUnit').selectedOptions[0];
      const entityId = Number(unitOption?.dataset?.parent || 0);
      const payload = {
        tenantId: state.tenant.id,
        entidadeId: entityId,
        unidadeId: Number($('ctxUnit').value),
        exercicioId: Number($('ctxExercise').value),
        sistemaId: Number($('ctxSystem').value),
        modoAcesso: $('ctxMode') ? $('ctxMode').value : 'OPERACIONAL',
        justificativa: $('ctxReason') ? $('ctxReason').value || null : null,
        versao: state.version
      };

      const spinner = $('ctxSpinner');
      if (spinner) spinner.classList.remove('d-none');

      try {
        const validation = await api('/api/saas/contexto/validar', {
          method: 'POST',
          body: JSON.stringify(payload)
        });
        if (validation.requiresJustification && (!$('ctxReason').value || $('ctxReason').value.trim().length < 15)) {
          state.requiresReason = true;
          const group = $('ctxReasonGroup');
          if (group) group.classList.remove('d-none');
          return error('Informe uma justificativa com pelo menos 15 caracteres.');
        }

        // Confirma a persistência no servidor
        await api('/api/saas/contexto/selecionar', {
          method: 'POST',
          body: JSON.stringify(payload)
        });

        // Transição completa: recarrega a aplicação no destino autorizado para reavaliar navegação e claims da sessão
        const params = new URLSearchParams(window.location.search);
        const returnUrl = params.get('returnUrl');
        if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')) {
          window.location.href = returnUrl;
        } else {
          window.location.href = '/';
        }
      } catch (x) {
        error(x.message);
      } finally {
        if (spinner) spinner.classList.add('d-none');
      }
    });
  }

  const returnGlobalBtn = $('ctxReturnGlobal');
  if (returnGlobalBtn) {
    returnGlobalBtn.addEventListener('click', async () => {
      if (!confirm('Voltar ao contexto global? Operações em andamento deverão ser revisadas no novo escopo.')) return;
      try {
        await api('/api/saas/contexto/global', { method: 'POST', body: '{}' });
        window.location.href = '/';
      } catch (e) {
        error(e.message);
      }
    });
  }

  const retryBtn = $('ctxRetry');
  if (retryBtn) retryBtn.addEventListener('click', current);

  current();
})();
