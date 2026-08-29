(function () {
  'use strict';
  const config = window.saude360Config;
  const status = document.getElementById('saude360-status');
  const wrap = document.getElementById('saude360-wrap');
  const body = document.getElementById('saude360-itens');
  const safe = value => { const node = document.createElement('span'); node.textContent = value == null ? '' : String(value); return node.innerHTML; };
  async function carregar() {
    status.classList.remove('d-none'); wrap.classList.add('d-none'); status.textContent = 'Carregando dados do contexto atual…';
    const q = document.getElementById('saude360-busca').value.trim();
    try {
      const response = await fetch(config.api + (q ? `?q=${encodeURIComponent(q)}` : ''), { credentials: 'same-origin', headers: { Accept: 'application/json' } });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      const payload = await response.json(); const data = payload.data || payload; const items = data.items || data.Items || (Array.isArray(data) ? data : []);
      body.innerHTML = items.map(x => `<tr><td>${safe(x.codigo || x.loteId || x.id || '—')}</td><td>${safe(x.nome || x.descricao || x.tipo || x.tema || 'Registro')}</td><td><span class="badge text-bg-secondary">${safe(x.status || x.situacao || 'ATIVO')}</span></td></tr>`).join('');
      if (!items.length) body.innerHTML = '<tr><td colspan="3"><div class="text-center py-4 text-muted">Nenhum registro encontrado para o contexto e filtros selecionados.</div></td></tr>';
      status.classList.add('d-none'); wrap.classList.remove('d-none');
    } catch (error) { status.textContent = `Não foi possível consultar a fonte oficial (${error.message}). Verifique permissão, contexto e schema.`; status.className = 'alert alert-danger mb-0'; }
  }
  document.getElementById('saude360-filtrar').addEventListener('click', carregar); carregar();
}());
