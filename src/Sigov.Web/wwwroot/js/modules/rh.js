(function () {
  const api = window.sigovApi;
  const token = () => $('input[name="__RequestVerificationToken"]').first().val();
  const antiforgeryHeaders = () => token() ? { RequestVerificationToken: token() } : {};

  async function loadDashboard() {
    if (!$('#rh-dashboard-cards').length) return;
    try {
      const res = await api.request('/api/rh/dashboard');
      const data = res.data || {};
      Object.keys(data).forEach(k => $(`[data-field="${k}"]`).text(data[k]));
    } catch (e) { window.sigovUi?.notify?.('Dashboard RH indisponível: ' + e.message, 'warning'); }
  }

  function currentRecurso() { return $('#rh-form').data('recurso'); }

  async function loadGrid() {
    const recurso = currentRecurso();
    if (!recurso) return;
    try {
      const res = await api.request(`/api/rh/${recurso}?page=1&pageSize=25`);
      const rows = (res.data && res.data.items) || [];
      $('#rh-grid').html(rows.map(r => `<tr><td>${r.id}</td><td>${r.ativo ? 'Sim' : 'Não'}</td><td><pre class="mb-0 small">${escapeHtml(JSON.stringify(r.dados, null, 2))}</pre></td><td><button class="btn btn-sm btn-outline-primary rh-edit" data-id="${r.id}">Editar</button> <button class="btn btn-sm btn-outline-danger rh-del" data-id="${r.id}">Excluir</button></td></tr>`).join(''));
    } catch (e) { window.sigovUi?.notify?.('Listagem RH indisponível: ' + e.message, 'warning'); }
  }

  async function save(e) {
    e.preventDefault();
    const recurso = currentRecurso();
    let dados;
    try { dados = JSON.parse($('#rh-json').val()); } catch { window.sigovUi?.notify?.('JSON inválido.', 'danger'); return; }
    const id = $('#rh-id').val();
    const method = id ? 'PUT' : 'POST';
    const path = id ? `/api/rh/${recurso}/${id}` : `/api/rh/${recurso}`;
    const body = id ? { dados, ativo: true } : { dados };
    await api.request(path, { method, headers: antiforgeryHeaders(), body: JSON.stringify(body) });
    $('#rh-id').val('');
    await loadGrid();
  }

  async function edit() {
    const recurso = currentRecurso();
    const id = $(this).data('id');
    const res = await api.request(`/api/rh/${recurso}/${id}`);
    $('#rh-id').val(id);
    $('#rh-json').val(JSON.stringify(res.data.dados || {}, null, 2));
  }

  async function del() {
    if (!confirm('Excluir logicamente este registro?')) return;
    await api.request(`/api/rh/${currentRecurso()}/${$(this).data('id')}`, { method: 'DELETE', headers: antiforgeryHeaders() });
    await loadGrid();
  }

  async function portal(e) {
    e.preventDefault();
    const id = $('#rh-servidor-id').val();
    const res = await api.request(`/api/rh/portal/servidores/${id}`);
    const p = res.data;
    $('#rh-portal-result').html(`<div class="card"><div class="card-body"><h2>${escapeHtml(p.nome)}</h2><h3>Contracheques</h3><pre>${escapeHtml(JSON.stringify(p.contracheques, null, 2))}</pre><h3>Férias</h3><pre>${escapeHtml(JSON.stringify(p.ferias, null, 2))}</pre><h3>Afastamentos</h3><pre>${escapeHtml(JSON.stringify(p.afastamentos, null, 2))}</pre></div></div>`);
  }

  function escapeHtml(s) { return String(s).replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[c])); }

  $(document).on('submit', '#rh-form', save);
  $(document).on('click', '.rh-edit', edit);
  $(document).on('click', '.rh-del', del);
  $(document).on('click', '#rh-clear', () => { $('#rh-id').val(''); $('#rh-json').val('{}'); });
  $(document).on('submit', '#rh-portal-form', portal);
  $(loadDashboard);
  $(loadGrid);
})();
