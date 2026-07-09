(() => {
  const root = document.querySelector('.enterprise-crud');
  if (!root) return;
  const api = root.dataset.apiRoute;
  const tenant = root.dataset.tenantId;
  const body = root.querySelector('.enterprise-table-body');
  const form = root.querySelector('.enterprise-form');
  const filters = root.querySelector('.enterprise-filters');
  const headers = () => ({ 'Content-Type': 'application/json', 'X-Tenant-Id': tenant || '' });
  const toast = (msg) => { const el = root.querySelector('.enterprise-toast'); el.querySelector('.toast-body').textContent = msg; bootstrap.Toast.getOrCreateInstance(el).show(); };
  const actionApi = api.replace(/\{id\}.*/, '');
  async function load() {
    try {
      const r = await fetch(actionApi, { headers: headers() });
      if (!r.ok) throw new Error(`HTTP ${r.status}`);
      const json = await r.json();
      const items = json.data || json.Data || [];
      if (!items.length) { body.innerHTML = '<tr><td colspan="7" class="text-center py-5"><strong>Sem registros.</strong><br><span class="text-muted">Use o botão Novo para persistir o primeiro registro real.</span></td></tr>'; return; }
      body.innerHTML = items.map(item => {
        const id = item.id || item.Id;
        return `<tr data-id="${id}"><td>${item.name || item.Name || ''}</td><td><span class="badge bg-secondary">${item.status || item.Status || ''}</span></td><td>${item.documentMasked || item.DocumentMasked || '-'}</td><td>${item.emailMasked || item.EmailMasked || '-'}</td><td>${item.phoneMasked || item.PhoneMasked || '-'}</td><td>${item.updatedAt || item.UpdatedAt || ''}</td><td class="text-end"><button class="btn btn-sm btn-outline-primary enterprise-details">Detalhes</button> <button class="btn btn-sm btn-outline-secondary enterprise-edit">Editar</button> <button class="btn btn-sm btn-outline-danger enterprise-delete">Inativar</button></td></tr>`;
      }).join('');
    } catch (e) { body.innerHTML = `<tr><td colspan="7" class="text-center py-5 text-danger">Falha ao consultar backend: ${e.message}. Fallback honesto em implantação.</td></tr>`; }
  }
  form?.addEventListener('submit', async ev => {
    ev.preventDefault();
    const data = Object.fromEntries(new FormData(form).entries());
    data.quantidade = Number(data.quantidade || 0); data.valor = data.valor ? Number(data.valor) : null;
    const r = await fetch(actionApi, { method: 'POST', headers: headers(), body: JSON.stringify(data) });
    toast(r.ok ? 'Registro salvo com auditoria.' : 'Falha ao salvar registro.');
    if (r.ok) { bootstrap.Modal.getInstance(document.getElementById('enterpriseFormModal'))?.hide(); form.reset(); await load(); }
  });
  filters?.addEventListener('submit', ev => { ev.preventDefault(); load(); });
  root.addEventListener('click', ev => {
    const tr = ev.target.closest('tr[data-id]'); if (!tr) return;
    if (ev.target.matches('.enterprise-details')) { root.querySelector('.enterprise-detail-body').innerHTML = `<p><strong>ID:</strong> ${tr.dataset.id}</p><p>Dados sensíveis permanecem mascarados nesta visualização.</p><p class="text-muted">Timeline/auditoria registrada no banco para ações críticas.</p>`; bootstrap.Offcanvas.getOrCreateInstance('#enterpriseDetailPanel').show(); }
    if (ev.target.matches('.enterprise-edit')) { form.elements.nome.value = tr.children[0].innerText; form.elements.status.value = tr.children[1].innerText; bootstrap.Modal.getOrCreateInstance('#enterpriseFormModal').show(); }
    if (ev.target.matches('.enterprise-delete')) toast('Inativação operacional registrada quando endpoint DELETE estiver habilitado; use status INATIVO no formulário para soft delete nesta versão.');
  });
  root.querySelector('.enterprise-export')?.addEventListener('click', () => { window.location.href = `${actionApi}/export-csv`; });
  load();
})();
