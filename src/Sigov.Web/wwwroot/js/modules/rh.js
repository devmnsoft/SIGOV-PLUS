(function () {
  const api = window.sigovApi;
  const token = () => $('input[name="__RequestVerificationToken"]').first().val();
  const antiforgeryHeaders = () => token() ? { RequestVerificationToken: token() } : {};

  const schemas = {
    servidores: [['matricula', 'Matrícula', 'text'], ['nome', 'Nome', 'text'], ['cpf', 'CPF', 'text'], ['emailInstitucional', 'E-mail institucional', 'email'], ['telefone', 'Telefone', 'tel']],
    cargos: [['codigo', 'Código', 'text'], ['nome', 'Nome', 'text'], ['cbo', 'CBO', 'text'], ['vencimentoBase', 'Vencimento base', 'number']],
    lotacoes: [['codigo', 'Código', 'text'], ['nome', 'Nome', 'text'], ['lotacaoPaiId', 'Lotação pai', 'number']],
    vinculos: [['servidorId', 'Servidor', 'number'], ['cargoId', 'Cargo', 'number'], ['lotacaoId', 'Lotação', 'number'], ['tipo', 'Tipo', 'text'], ['dataAdmissao', 'Admissão', 'date']],
    folhas: [['ano', 'Ano', 'number'], ['mes', 'Mês', 'number'], ['tipo', 'Tipo', 'text'], ['status', 'Status', 'text']],
    'folha-eventos': [['codigo', 'Código', 'text'], ['descricao', 'Descrição', 'text'], ['tipo', 'Tipo', 'text'], ['incideInss', 'Incide INSS', 'checkbox'], ['incideIrrf', 'Incide IRRF', 'checkbox']],
    'folha-lancamentos': [['folhaId', 'Folha', 'number'], ['servidorId', 'Servidor', 'number'], ['eventoId', 'Evento', 'number'], ['valor', 'Valor', 'number']],
    pontos: [['servidorId', 'Servidor', 'number'], ['dataHora', 'Data/hora', 'datetime-local'], ['tipo', 'Tipo', 'text'], ['origem', 'Origem', 'text']],
    ferias: [['servidorId', 'Servidor', 'number'], ['inicio', 'Início', 'date'], ['fim', 'Fim', 'date'], ['status', 'Status', 'text']],
    afastamentos: [['servidorId', 'Servidor', 'number'], ['inicio', 'Início', 'date'], ['fim', 'Fim', 'date'], ['motivo', 'Motivo', 'text'], ['status', 'Status', 'text']],
    'saude-ocupacional': [['servidorId', 'Servidor', 'number'], ['tipo', 'Tipo', 'text'], ['dataAtendimento', 'Data atendimento', 'date'], ['status', 'Status', 'text']],
    esocial: [['evento', 'Evento eSocial', 'text'], ['servidorId', 'Servidor', 'number'], ['status', 'Status', 'text'], ['recibo', 'Recibo', 'text']]
  };

  async function loadDashboard() {
    if (!$('#rh-dashboard-cards').length) return;
    try {
      const res = await api.request('/api/rh/dashboard');
      const data = res.data || {};
      Object.keys(data).forEach(k => $(`[data-field="${k}"]`).text(data[k]));
    } catch (e) { notifyHttpError(e, 'Dashboard RH indisponível'); }
  }

  function currentRecurso() { return $('#rh-form').data('recurso'); }

  function renderFields() {
    const recurso = currentRecurso();
    const schema = schemas[recurso] || [];
    if (!schema.length) return;
    $('#rh-fields').html(schema.map(([key, label, type]) => {
      const required = ['matricula', 'nome', 'cpf', 'codigo', 'servidorId', 'status', 'tipo'].includes(key) ? 'required' : '';
      if (type === 'checkbox') return `<div class="col-md-3 form-check mt-4"><input class="form-check-input rh-field" id="rh-${key}" data-key="${key}" type="checkbox"><label class="form-check-label" for="rh-${key}">${label}</label></div>`;
      return `<div class="col-md-3"><label class="form-label" for="rh-${key}">${label}</label><input class="form-control rh-field" id="rh-${key}" data-key="${key}" type="${type}" ${required}></div>`;
    }).join(''));
  }

  function formToDados() {
    const dados = {};
    $('.rh-field').each(function () {
      const key = $(this).data('key');
      const type = $(this).attr('type');
      if (type === 'checkbox') { dados[key] = $(this).is(':checked'); return; }
      const value = $(this).val();
      if (value === '') return;
      dados[key] = type === 'number' ? Number(value) : value;
    });
    const extra = $('#rh-json').val().trim();
    if (extra) Object.assign(dados, JSON.parse(extra));
    return dados;
  }

  function dadosToForm(dados) {
    $('.rh-field').each(function () {
      const key = $(this).data('key');
      if (!Object.prototype.hasOwnProperty.call(dados, key)) return;
      if ($(this).attr('type') === 'checkbox') $(this).prop('checked', Boolean(dados[key]));
      else $(this).val(dados[key]);
    });
    const extras = { ...dados };
    $('.rh-field').each(function () { delete extras[$(this).data('key')]; });
    $('#rh-json').val(JSON.stringify(extras, null, 2));
  }

  async function loadGrid(recursoOverride) {
    const recurso = recursoOverride || currentRecurso();
    if (!recurso) return;
    try {
      const res = await api.request(`/api/rh/${recurso}?page=1&pageSize=25`);
      const rows = (res.data && res.data.items) || [];
      if (!rows.length) { $('#rh-grid').html('<tr><td colspan="4"><div class="text-center text-muted py-4">Nenhum registro encontrado para os filtros atuais.</div></td></tr>'); return; }
      $('#rh-grid').html(rows.map(r => `<tr><td>${r.id}</td><td>${r.ativo ? 'Sim' : 'Não'}</td><td><pre class="mb-0 small">${escapeHtml(JSON.stringify(r.dados, null, 2))}</pre></td><td><button class="btn btn-sm btn-outline-primary rh-edit" data-id="${r.id}">Editar</button> <button class="btn btn-sm btn-outline-danger rh-del" data-id="${r.id}">Excluir</button></td></tr>`).join(''));
    } catch (e) { notifyHttpError(e, 'Listagem RH indisponível'); }
  }

  async function save(e) {
    e.preventDefault();
    const recurso = currentRecurso();
    let dados;
    try { dados = formToDados(); } catch { window.sigovUi?.notify?.('JSON complementar inválido.', 'danger'); return; }
    const id = $('#rh-id').val();
    const method = id ? 'PUT' : 'POST';
    const path = id ? `/api/rh/${recurso}/${id}` : `/api/rh/${recurso}`;
    const body = id ? { dados, ativo: true } : { dados };
    try {
      await api.request(path, { method, headers: antiforgeryHeaders(), body: JSON.stringify(body) });
      window.sigovUi?.notify?.('Registro RH salvo com sucesso.', 'success');
      clearForm();
      await loadGrid();
    } catch (e) { notifyHttpError(e, 'Não foi possível salvar o registro RH'); }
  }

  async function edit() {
    const recurso = currentRecurso();
    const id = $(this).data('id');
    const res = await api.request(`/api/rh/${recurso}/${id}`);
    $('#rh-id').val(id);
    dadosToForm(res.data.dados || {});
  }

  async function del() {
    if (!confirm('Excluir logicamente este registro?')) return;
    try {
      await api.request(`/api/rh/${currentRecurso()}/${$(this).data('id')}`, { method: 'DELETE', headers: antiforgeryHeaders() });
      window.sigovUi?.notify?.('Registro RH excluído logicamente.', 'success');
      await loadGrid();
    } catch (e) { notifyHttpError(e, 'Não foi possível excluir o registro RH'); }
  }

  async function portal(e) {
    e.preventDefault();
    const id = $('#rh-servidor-id').val();
    let res;
    try { res = await api.request(`/api/rh/portal/servidores/${id}`); } catch (e) { notifyHttpError(e, 'Portal do servidor indisponível'); return; }
    const p = res.data;
    $('#rh-portal-result').html(`<div class="card"><div class="card-body"><h2>${escapeHtml(p.nome)}</h2><h3>Contracheques</h3><pre>${escapeHtml(JSON.stringify(p.contracheques, null, 2))}</pre><h3>Férias</h3><pre>${escapeHtml(JSON.stringify(p.ferias, null, 2))}</pre><h3>Afastamentos</h3><pre>${escapeHtml(JSON.stringify(p.afastamentos, null, 2))}</pre></div></div>`);
  }

  async function integrarFinanceiro(e) {
    e.preventDefault();
    const body = {
      folhaId: Number($('#rh-integrar-folha-id').val()),
      dataCompetencia: $('#rh-integrar-competencia').val(),
      naturezaDespesaId: Number($('#rh-integrar-natureza-id').val()) || null,
      fonteRecursoId: Number($('#rh-integrar-fonte-id').val()) || null,
      historico: $('#rh-integrar-historico').val()
    };
    try {
      const res = await api.request('/api/rh/folhas/integrar-financeiro', { method: 'POST', headers: antiforgeryHeaders(), body: JSON.stringify(body) });
      window.sigovUi?.notify?.(`Evento outbox #${res.data} preparado para Financeiro/SIAFIC.`, 'success');
    } catch (e) { notifyHttpError(e, 'Não foi possível integrar a folha ao Financeiro'); }
  }

  function clearForm() { $('#rh-id').val(''); $('.rh-field').val('').prop('checked', false); $('#rh-json').val('{}'); }
  function escapeHtml(s) { return String(s).replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c])); }

  function notifyHttpError(e, prefix) {
    const status = e && (e.status || e.statusCode);
    if (status === 401) { window.sigovUi?.notify?.('Sessão expirada. Faça login novamente.', 'warning'); return; }
    if (status === 403) { window.sigovUi?.notify?.('Você não tem permissão para esta operação de RH.', 'warning'); return; }
    if (status === 404) { window.sigovUi?.notify?.('Registro de RH não encontrado ou indisponível para este tenant.', 'warning'); return; }
    if (status === 409) { window.sigovUi?.notify?.('Conflito de regra de negócio em RH. Verifique duplicidade, status ou competência.', 'warning'); return; }
    if (status === 422) { window.sigovUi?.notify?.('Validação de RH não atendida. Revise campos obrigatórios e formatos.', 'warning'); return; }
    window.sigovUi?.notify?.(`${prefix}: erro inesperado tratado sem expor detalhes técnicos.`, 'warning');
  }

  async function postTyped(path, payload) {
    return api.request(path, { method: 'POST', headers: antiforgeryHeaders(), body: JSON.stringify(payload || {}) });
  }

  async function getTyped(path) { return api.request(path); }

  async function deleteTyped(path) { return api.request(path, { method: 'DELETE', headers: antiforgeryHeaders() }); }

  window.SigovRh = { loadDashboard, loadGrid, postTyped, getTyped, deleteTyped, carregarDashboardRh: loadDashboard, carregarServidores: () => loadGrid('servidores'), carregarFolhas: () => loadGrid('folhas') };

  $(document).on('submit', '#rh-form', save);
  $(document).on('submit', '#rh-integrar-form', integrarFinanceiro);
  $(document).on('click', '.rh-edit', edit);
  $(document).on('click', '.rh-del', del);
  $(document).on('click', '#rh-clear', clearForm);
  $(document).on('submit', '#rh-portal-form', portal);
  $(function () { renderFields(); loadDashboard(); loadGrid(); });
})();
