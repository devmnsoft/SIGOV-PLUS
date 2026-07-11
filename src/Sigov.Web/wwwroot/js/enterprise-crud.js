(() => {
  const root = document.querySelector('.enterprise-crud');
  if (!root) return;

  const api = (root.dataset.apiRoute || '').replace(/\/$/, '');
  const tenant = root.dataset.tenantId || '';
  const body = root.querySelector('.enterprise-table-body');
  const form = root.querySelector('.enterprise-form');
  const filters = root.querySelector('.enterprise-filters');
  const exportButton = root.querySelector('.enterprise-export');
  const newButton = root.querySelector('.enterprise-new');
  const pager = root.querySelector('.enterprise-pager');
  const statusText = root.querySelector('.enterprise-status');
  const modalElement = document.getElementById('enterpriseFormModal');
  const titleElement = modalElement?.querySelector('.modal-title');
  let currentItems = [];
  let currentPage = 1;
  const pageSize = 20;
  const areaKey = (api.split('/').filter(Boolean).pop() || 'default').toLowerCase();
  const metadata = (window.SigovEnterpriseFormMetadata && (window.SigovEnterpriseFormMetadata[areaKey] || window.SigovEnterpriseFormMetadata.default)) || { actions: [] };

  const headers = (json = true) => {
    const h = { 'X-Tenant-Id': tenant };
    if (json) h['Content-Type'] = 'application/json';
    return h;
  };
  const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c]));
  const toast = (msg, isError = false) => {
    const el = root.querySelector('.enterprise-toast');
    if (!el || !window.bootstrap) return;
    el.querySelector('.toast-body').textContent = msg;
    el.classList.toggle('text-bg-danger', isError);
    el.classList.toggle('text-bg-success', !isError);
    bootstrap.Toast.getOrCreateInstance(el).show();
  };
  const getItemValue = (item, ...names) => names.map(n => item[n]).find(v => v !== undefined && v !== null) ?? '';
  const actionApi = api.replace(/\/dashboard$/i, '');
  const buildQuery = () => {
    const params = new URLSearchParams(new FormData(filters || undefined));
    params.set('page', String(currentPage));
    params.set('pageSize', String(pageSize));
    const status = params.get('status');
    if (status) params.set('search', status);
    return params.toString();
  };
  const setBusy = busy => {
    root.classList.toggle('enterprise-busy', busy);
    root.querySelectorAll('button, input, select').forEach(el => { if (!el.closest('.toast')) el.disabled = busy; });
  };
  async function load() {
    setBusy(true);
    body.innerHTML = '<tr><td colspan="7" class="text-center py-5 text-muted"><span class="spinner-border spinner-border-sm me-2"></span>Consultando dados do tenant...</td></tr>';
    try {
      const url = `${actionApi}?${buildQuery()}`;
      const r = await fetch(url, { headers: headers(false) });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const json = await r.json();
      currentItems = json.data || json.Data || [];
      renderRows();
      if (statusText) statusText.textContent = `${currentItems.length} registro(s) carregado(s), página ${currentPage}.`;
    } catch (e) {
      body.innerHTML = `<tr><td colspan="7" class="text-center py-5 text-danger"><strong>Falha ao consultar backend.</strong><br>${escapeHtml(e.message)}. Fallback honesto: valide migrations, tenant e logs antes de homologar.</td></tr>`;
      toast('Falha ao consultar backend Enterprise.', true);
    } finally { setBusy(false); }
  }
  function renderRows() {
    if (!currentItems.length) {
      body.innerHTML = '<tr><td colspan="7" class="text-center py-5"><strong>Nenhum registro encontrado.</strong><br><span class="text-muted">Ajuste os filtros ou use Novo para persistir um registro real.</span></td></tr>';
      return;
    }
    body.innerHTML = currentItems.map(item => {
      const id = getItemValue(item, 'id', 'Id');
      const status = getItemValue(item, 'status', 'Status') || 'ATIVO';
      const deleted = String(status).toUpperCase() === 'INATIVO';
      return `<tr data-id="${escapeHtml(id)}"><td><strong>${escapeHtml(getItemValue(item, 'name', 'Name', 'nome', 'Nome'))}</strong><div class="small text-muted">${escapeHtml(id)}</div></td><td><span class="badge ${deleted ? 'bg-warning text-dark' : 'bg-secondary'}">${escapeHtml(status)}</span></td><td>${escapeHtml(getItemValue(item, 'documentMasked', 'DocumentMasked')) || '-'}</td><td>${escapeHtml(getItemValue(item, 'emailMasked', 'EmailMasked')) || '-'}</td><td>${escapeHtml(getItemValue(item, 'phoneMasked', 'PhoneMasked')) || '-'}</td><td>${escapeHtml(getItemValue(item, 'updatedAt', 'UpdatedAt'))}</td><td class="text-end text-nowrap"><button class="btn btn-sm btn-outline-primary enterprise-details">Detalhes</button> <button class="btn btn-sm btn-outline-secondary enterprise-edit">Editar</button> <button class="btn btn-sm btn-outline-danger enterprise-delete">Inativar</button> <button class="btn btn-sm btn-outline-success enterprise-restore">Restaurar</button> ${metadata.actions.map(a => `<button class="btn btn-sm btn-outline-dark enterprise-op" data-action="${escapeHtml(a.key)}">${escapeHtml(a.label)}</button>`).join(' ')}</td></tr>`;
    }).join('');
  }
  function fillForm(item = {}) {
    form.reset();
    form.elements.id.value = getItemValue(item, 'id', 'Id');
    form.elements.nome.value = getItemValue(item, 'name', 'Name', 'nome', 'Nome');
    form.elements.status.value = getItemValue(item, 'status', 'Status') || 'ATIVO';
    titleElement.textContent = form.elements.id.value ? 'Editar registro' : 'Novo registro';
  }
  async function submitForm(ev) {
    ev.preventDefault();
    const data = Object.fromEntries(new FormData(form).entries());
    data.quantidade = Number(data.quantidade || 0);
    data.valor = data.valor ? Number(data.valor) : null;
    const id = data.id;
    delete data.id;
    setBusy(true);
    try {
      const r = await fetch(id ? `${actionApi}/${id}` : actionApi, { method: id ? 'PUT' : 'POST', headers: headers(), body: JSON.stringify(data) });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      toast('Registro salvo com auditoria.');
      bootstrap.Modal.getInstance(modalElement)?.hide();
      await load();
    } catch (e) { toast(`Falha ao salvar: ${e.message}`, true); }
    finally { setBusy(false); }
  }
  async function lifecycle(id, restore) {
    if (!confirm(restore ? 'Confirmar restauração deste registro?' : 'Confirmar inativação deste registro?')) return;
    const url = restore ? `${actionApi}/${id}/restaurar` : `${actionApi}/${id}`;
    const r = await fetch(url, { method: restore ? 'POST' : 'DELETE', headers: headers(false) });
    toast(r.ok ? (restore ? 'Registro restaurado com auditoria.' : 'Registro inativado com auditoria.') : 'Falha na ação de ciclo de vida.', !r.ok);
    if (r.ok) await load();
  }
  form?.addEventListener('submit', submitForm);
  filters?.addEventListener('submit', ev => { ev.preventDefault(); currentPage = 1; load(); });
  newButton?.addEventListener('click', () => fillForm());
  pager?.addEventListener('click', ev => { const btn = ev.target.closest('[data-page]'); if (!btn) return; currentPage = Math.max(1, currentPage + Number(btn.dataset.page)); filters.elements.page.value = currentPage; load(); });
  root.addEventListener('click', ev => {
    const tr = ev.target.closest('tr[data-id]'); if (!tr) return;
    const item = currentItems.find(x => String(getItemValue(x, 'id', 'Id')) === tr.dataset.id) || {};
    if (ev.target.matches('.enterprise-details')) {
      root.querySelector('.enterprise-detail-body').innerHTML = `<dl class="row"><dt class="col-sm-4">ID</dt><dd class="col-sm-8 text-break">${escapeHtml(tr.dataset.id)}</dd><dt class="col-sm-4">Nome</dt><dd class="col-sm-8">${escapeHtml(getItemValue(item, 'name', 'Name'))}</dd><dt class="col-sm-4">Status</dt><dd class="col-sm-8">${escapeHtml(getItemValue(item, 'status', 'Status'))}</dd></dl><p class="alert alert-info small">Dados sensíveis permanecem mascarados. Ações críticas registram auditoria operacional por tenant.</p>`;
      bootstrap.Offcanvas.getOrCreateInstance(document.getElementById('enterpriseDetailPanel')).show();
    }
    if (ev.target.matches('.enterprise-edit')) { fillForm(item); bootstrap.Modal.getOrCreateInstance(modalElement).show(); }
    if (ev.target.matches('.enterprise-delete')) lifecycle(tr.dataset.id, false);
    if (ev.target.matches('.enterprise-restore')) lifecycle(tr.dataset.id, true);
    if (ev.target.matches('.enterprise-op')) operational(tr.dataset.id, ev.target.dataset.action);
  });
  async function operational(id, action) {
    if (!action || !confirm(`Confirmar ação operacional: ${action}?`)) return;
    setBusy(true);
    try {
      const r = await fetch(`${actionApi}/${id}/${action}`, { method: 'POST', headers: headers(), body: JSON.stringify({ quantidade: 1 }) });
      const payload = await r.text();
      if (!r.ok) throw new Error(payload || `HTTP ${r.status}`);
      toast('Ação operacional executada com auditoria.');
      await load();
    } catch (e) { toast(`Ação bloqueada: ${e.message}`, true); }
    finally { setBusy(false); }
  }
  exportButton?.addEventListener('click', async () => {
    const r = await fetch(`${actionApi}/export-csv?${buildQuery()}`, { headers: headers(false) });
    if (!r.ok) return toast('Falha ao exportar CSV.', true);
    const blob = await r.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = `enterprise-${new Date().toISOString().replace(/[:.]/g, '-')}.csv`;
    a.click();
    URL.revokeObjectURL(a.href);
    toast('CSV exportado com filtros e LGPD.');
  });
  load();
})();
