(function () {
  'use strict';
  const palette = document.getElementById('sigovCommandPalette');
  if (!palette) return;
  const backdrop = document.querySelector('.sigov-command-backdrop');
  const search = palette.querySelector('#sigovCommandSearch');
  const links = Array.from(palette.querySelectorAll('[data-command]'));
  const empty = palette.querySelector('[data-sigov-command-empty]');
  let previousFocus = null;
  function visibleLinks() { return links.filter(link => !link.hidden); }
  function open(quickCreate) {
    previousFocus = document.activeElement; palette.hidden = false; backdrop.hidden = false;
    document.body.classList.add('sigov-command-open'); search.value = quickCreate ? 'novo' : ''; filter();
    window.requestAnimationFrame(() => search.focus());
  }
  function close() { palette.hidden = true; backdrop.hidden = true; document.body.classList.remove('sigov-command-open'); previousFocus?.focus(); }
  function filter() {
    const term = search.value.trim().toLocaleLowerCase('pt-BR');
    links.forEach(link => { link.hidden = Boolean(term) && !link.dataset.command.toLocaleLowerCase('pt-BR').includes(term); });
    empty.hidden = visibleLinks().length !== 0;
  }
  document.querySelectorAll('[data-sigov-command-open]').forEach(button => button.addEventListener('click', () => open(false)));
  document.querySelectorAll('[data-sigov-quick-create]').forEach(button => button.addEventListener('click', () => open(true)));
  document.querySelectorAll('[data-sigov-command-close]').forEach(button => button.addEventListener('click', close));
  search.addEventListener('input', filter);
  document.addEventListener('keydown', event => {
    if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') { event.preventDefault(); palette.hidden ? open(false) : close(); return; }
    if (palette.hidden) return;
    if (event.key === 'Escape') { event.preventDefault(); close(); return; }
    const available = visibleLinks(), current = available.indexOf(document.activeElement);
    if (event.key === 'ArrowDown') { event.preventDefault(); (available[current + 1] || available[0])?.focus(); }
    if (event.key === 'ArrowUp') { event.preventDefault(); (available[current - 1] || available.at(-1))?.focus(); }
    if (event.key === 'Tab') {
      const focusable = [search, ...available, palette.querySelector('[data-sigov-command-close]')];
      const edge = event.shiftKey ? focusable[0] : focusable.at(-1);
      if (document.activeElement === edge) { event.preventDefault(); (event.shiftKey ? focusable.at(-1) : focusable[0]).focus(); }
    }
  });
})();
