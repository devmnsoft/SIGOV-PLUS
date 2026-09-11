(function () {
  'use strict';
  const palette = document.getElementById('sigovCommandPalette');
  if (!palette) return;
  const backdrop = document.querySelector('.sigov-command-backdrop');
  const search = palette.querySelector('#sigovCommandSearch');
  const results = palette.querySelector('[data-sigov-command-results]');
  const empty = palette.querySelector('[data-sigov-command-empty]');
  const skeleton = palette.querySelector('[data-sigov-command-loading]');
  let previousFocus = null, timer = 0;
  const safeUrl = value => window.SigovNavigationState?.safeInternalPath(value);
  function item(row) {
    const url = safeUrl(row.url); if (!url) return null;
    const link = document.createElement('a'); link.className = 'sigov-command-item'; link.href = url;
    link.dataset.command = [row.area, row.titulo, row.descricao, row.badge].filter(Boolean).join(' ').toLocaleLowerCase('pt-BR');
    const iconBox = document.createElement('span'); iconBox.className = 'sigov-action-card__icon';
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg'); svg.setAttribute('width', '18'); svg.setAttribute('height', '18');
    const use = document.createElementNS('http://www.w3.org/2000/svg', 'use'); use.setAttribute('href', `#sigov-icon-${row.icon || 'dashboard'}`); svg.appendChild(use); iconBox.appendChild(svg);
    const text = document.createElement('span'); const title = document.createElement('strong'); title.textContent = row.titulo || '';
    const description = document.createElement('small'); description.textContent = row.descricao || ''; text.append(title, description);
    const shortcut = document.createElement('kbd'); shortcut.textContent = row.atalho || ''; link.append(iconBox, text, shortcut); return link;
  }
  function links() { return Array.from(palette.querySelectorAll('.sigov-command-item')).filter(link => !link.hidden); }
  function filter() { const term = search.value.trim().toLocaleLowerCase('pt-BR'); palette.querySelectorAll('.sigov-command-item').forEach(link => { link.hidden = Boolean(term) && !link.dataset.command.includes(term); }); empty.hidden = links().length !== 0; }
  function render(rows) { results.querySelectorAll('.sigov-command-item').forEach(node => node.remove()); rows.map(item).filter(Boolean).forEach(node => results.insertBefore(node, empty)); filter(); }
  async function fetchSuggestions(query) {
    skeleton.hidden = false;
    try { const response = await fetch(`/Busca/Sugestoes?q=${encodeURIComponent(query || '')}`, { headers: { Accept: 'application/json' } }); if (!response.ok) throw new Error(`HTTP ${response.status}`); const data = await response.json(); render(Array.isArray(data.resultados) ? data.resultados : []); }
    catch { render([]); window.SigovToast?.warning('A busca de comandos está indisponível. Tente novamente.', 'Busca indisponível'); }
    finally { skeleton.hidden = true; }
  }
  function open() { previousFocus = document.activeElement; palette.hidden = false; backdrop.hidden = false; document.body.classList.add('sigov-command-open'); search.value = ''; fetchSuggestions(''); requestAnimationFrame(() => search.focus()); }
  function close() { palette.hidden = true; backdrop.hidden = true; document.body.classList.remove('sigov-command-open'); previousFocus?.focus(); }
  document.querySelectorAll('[data-sigov-command-open]').forEach(button => button.addEventListener('click', open));
  document.querySelectorAll('[data-sigov-command-close]').forEach(button => button.addEventListener('click', close));
  search.addEventListener('input', () => { filter(); clearTimeout(timer); timer = setTimeout(() => fetchSuggestions(search.value), 220); });
  document.addEventListener('keydown', event => { if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); palette.hidden ? open() : close(); return; } if (palette.hidden) return; if (event.key === 'Escape') { event.preventDefault(); close(); return; } const available = links(), current = available.indexOf(document.activeElement); if (event.key === 'ArrowDown') { event.preventDefault(); (available[current + 1] || available[0])?.focus(); } if (event.key === 'ArrowUp') { event.preventDefault(); (available[current - 1] || available.at(-1))?.focus(); } });
  window.SigovCommandPalette = { open, close };
})();
