(() => {
  const root = document.querySelector('.edu-b3');
  if (!root) return;
  const table = root.querySelector('table'), body = root.querySelector('tbody'), empty = root.querySelector('.empty-state'), loading = root.querySelector('[data-loading]');
  const text = value => value == null ? '—' : String(value);
  async function load() {
    loading.hidden = false; table.hidden = true; empty.hidden = true;
    try {
      const response = await fetch(root.dataset.endpoint, { headers: { Accept: 'application/json' }, credentials: 'same-origin' });
      if (!response.ok) throw new Error('Acesso indisponível');
      const envelope = await response.json(); const source = envelope.data ?? envelope;
      const rows = Array.isArray(source) ? source : Object.values(source).flatMap(x => Array.isArray(x) ? x : []);
      body.replaceChildren(...rows.map(row => { const tr=document.createElement('tr'); [row.id,row.tipo ?? row.titulo ?? row.periodo,row.status ?? 'Disponível',row.createdAt ?? row.dataOcorrencia].forEach(v=>{const td=document.createElement('td');td.textContent=text(v);tr.append(td);}); return tr; }));
      root.querySelector('[data-total]').textContent = rows.length; table.hidden = rows.length === 0; empty.hidden = rows.length !== 0;
    } catch { empty.querySelector('strong').textContent='Não foi possível carregar'; empty.hidden=false; }
    finally { loading.hidden=true; }
  }
  root.querySelector('[data-reload]').addEventListener('click', load);
  root.querySelector('[data-filter]').addEventListener('input', e => [...body.rows].forEach(r => r.hidden=!r.textContent.toLowerCase().includes(e.target.value.toLowerCase())));
  load();
})();
