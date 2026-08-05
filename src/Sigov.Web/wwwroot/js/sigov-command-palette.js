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
  const fallback = [
    ['Navegação','Minha Central','Resumo operacional do usuário','/MinhaCentral','home','Ctrl+H'],['Navegação','Dashboard','Visão executiva SaaS','/Dashboard','dashboard','Ctrl+D'],
    ['Governo','Protocolo','Processos e tramitação','/Protocolo','protocol','P'],['Governo','GED/OCR','Documentos e OCR','/Ged/Dashboard','documents','G'],
    ['Administração','LGPD','Privacidade e dados pessoais','/Lgpd/Dashboard','shield','L'],['Administração','Auditoria','Trilhas e eventos','/Auditoria/Trilhas','audit','A'],
    ['Ações','Novo protocolo','Abrir fluxo de criação rápida','/Protocolo','plus','N P'],['Operação','Health','Saúde operacional','/Operacao/Health','active','H']
  ];
  function item(row){ const a=document.createElement('a'); a.className='sigov-command-item'; a.href=row.url||row[3]; a.dataset.command=[row.area||row[0],row.titulo||row[1],row.descricao||row[2],row.badge||''].join(' ').toLowerCase(); a.innerHTML=`<span class="sigov-action-card__icon"><svg width="18" height="18"><use href="#sigov-icon-${row.icon||row[4]||'dashboard'}"></use></svg></span><span><strong>${row.titulo||row[1]}</strong><small>${row.descricao||row[2]}</small></span><kbd>${row.atalho||row[5]||''}</kbd>`; return a; }
  function render(rows){ results.querySelectorAll('.sigov-command-item').forEach(x=>x.remove()); rows.forEach(r=>results.insertBefore(item(r), empty)); filter(); }
  async function fetchSuggestions(q){ skeleton.hidden=false; try{ const res=await fetch(`/Busca/Sugestoes?q=${encodeURIComponent(q||'')}`,{headers:{'Accept':'application/json'}}); if(!res.ok) throw new Error('HTTP '+res.status); const data=await res.json(); render((data.resultados&&data.resultados.length?data.resultados:null)||fallback.map(x=>({area:x[0],titulo:x[1],descricao:x[2],url:x[3],icon:x[4],atalho:x[5]}))); }catch(e){ render(fallback); window.SigovToast?.info('Busca em fallback','Sugestões locais exibidas sem acessar dados sensíveis.'); } finally{skeleton.hidden=true;} }
  function links(){ return Array.from(palette.querySelectorAll('.sigov-command-item')).filter(link => !link.hidden); }
  function open() { previousFocus = document.activeElement; palette.hidden = false; backdrop.hidden = false; document.body.classList.add('sigov-command-open'); search.value=''; fetchSuggestions(''); requestAnimationFrame(()=>search.focus()); }
  function close() { palette.hidden = true; backdrop.hidden = true; document.body.classList.remove('sigov-command-open'); previousFocus?.focus(); }
  function filter(){ const term=search.value.trim().toLocaleLowerCase('pt-BR'); links().forEach(()=>{}); palette.querySelectorAll('.sigov-command-item').forEach(link=>{link.hidden=!!term&&!link.dataset.command.includes(term)}); empty.hidden=links().length!==0; }
  document.querySelectorAll('[data-sigov-command-open]').forEach(button=>button.addEventListener('click', open));
  document.querySelectorAll('[data-sigov-command-close]').forEach(button=>button.addEventListener('click', close));
  search.addEventListener('input',()=>{filter(); clearTimeout(timer); timer=setTimeout(()=>fetchSuggestions(search.value),220)});
  palette.addEventListener('click', event=>{ const quick=event.target.closest('a[href="#quick-create"]'); if(quick){ event.preventDefault(); close(); document.querySelector('[data-sigov-quick-create]')?.click(); }});
  document.addEventListener('keydown', event=>{ if((event.ctrlKey||event.metaKey)&&event.key.toLowerCase()==='k'){event.preventDefault(); palette.hidden?open():close(); return;} if(palette.hidden) return; if(event.key==='Escape'){event.preventDefault();close();return;} const available=links(), current=available.indexOf(document.activeElement); if(event.key==='ArrowDown'){event.preventDefault();(available[current+1]||available[0])?.focus();} if(event.key==='ArrowUp'){event.preventDefault();(available[current-1]||available.at(-1))?.focus();} });
  window.SigovCommandPalette={open,close};
})();
