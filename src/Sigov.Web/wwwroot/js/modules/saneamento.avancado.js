(() => {
  const root = document.querySelector('[data-sanitation-module]'); if (!root) return;
  const moduleName = root.dataset.sanitationModule; const resource = root.dataset.sanitationResource;
  const rows = root.querySelector('[data-sanitation-rows]'); const empty = root.querySelector('[data-sanitation-empty]');
  const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, char => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[char]));
  async function load() {
    rows.innerHTML = '<tr class="loading-skeleton"><td colspan="6">Carregando dados reais…</td></tr>'; empty.hidden = true;
    const status = root.querySelector('[data-sanitation-status]').value; const endpoint = resource === 'dashboard' ? `dashboard` : `${resource}?status=${encodeURIComponent(status)}`;
    try { const response = await fetch(`/api/saneamento/${moduleName}/${endpoint}`, {headers:{Accept:'application/json'}}); if (!response.ok) throw new Error('Não foi possível consultar o saneamento.'); const envelope = await response.json(); const data = envelope.data ?? envelope; const items = data.items ?? data.recentes ?? [];
      ['total','ativos','pendentes','alertas'].forEach(key => { const node=root.querySelector(`[data-kpi="${key}"]`); if(node) node.textContent=data[key] ?? 0; });
      const term = root.querySelector('[data-sanitation-search]').value.toLocaleLowerCase('pt-BR'); const filtered=items.filter(x=>!term||JSON.stringify(x).toLocaleLowerCase('pt-BR').includes(term));
      rows.innerHTML=filtered.map(x=>`<tr><td><strong>${escapeHtml(x.codigo||x.numero||x.id)}</strong></td><td>${escapeHtml(x.tipo||'—')}</td><td><span class="status-chip">${escapeHtml(x.status)}</span></td><td>${escapeHtml(x.descricao||'—')}</td><td>${escapeHtml(x.valor??'—')}</td><td><button class="btn btn-sm btn-outline-primary" type="button" aria-label="Consultar registro ${escapeHtml(x.id)}">Detalhes</button></td></tr>`).join(''); empty.hidden=filtered.length>0; if(!filtered.length) rows.innerHTML='';
    } catch(error) { rows.innerHTML=`<tr><td colspan="6">${escapeHtml(error.message)} Estrutura RC50.50 pode estar pendente.</td></tr>`; }
  }
  root.querySelector('[data-sanitation-refresh]').addEventListener('click',load); root.querySelector('[data-sanitation-filter]').addEventListener('click',load); load();
})();
