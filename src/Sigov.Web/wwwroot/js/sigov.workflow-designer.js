(() => {
  'use strict';
  const root = document.querySelector('.rc49-designer');
  if (!root || root.dataset.readonly === 'true') return;
  const canvas = root.querySelector('[data-canvas]');
  const drawer = root.querySelector('[data-drawer]');
  let selected = null;
  const toast = (message, error = false) => {
    const node = document.createElement('div'); node.className = `rc49-toast ${error ? 'rc49-toast--error' : ''}`; node.setAttribute('role', 'status'); node.textContent = message; document.body.append(node); setTimeout(() => node.remove(), 3500);
  };
  const select = card => {
    selected?.classList.remove('is-selected'); selected = card; card.classList.add('is-selected');
    drawer.querySelector('p').hidden = true; const fields = drawer.querySelector('[data-properties]'); fields.hidden = false;
    fields.querySelector('[data-field="name"]').value = card.dataset.name; fields.querySelector('[data-field="type"]').value = card.dataset.type; fields.querySelector('[data-field="initial"]').checked = card.dataset.initial === 'true'; fields.querySelector('[data-field="final"]').checked = card.dataset.final === 'true';
  };
  canvas.addEventListener('click', event => { const card = event.target.closest('[data-step]'); if (card) select(card); });
  drawer.addEventListener('input', event => { if (!selected) return; const field = event.target.dataset.field; const value = event.target.type === 'checkbox' ? String(event.target.checked) : event.target.value; selected.dataset[field] = value; if(field === 'name') selected.querySelector('h2').textContent = value; if(field === 'type') selected.querySelector('span').textContent = value; });
  root.querySelector('[data-add-step]')?.addEventListener('click', () => { canvas.querySelector('.rc49-empty')?.remove(); const count=canvas.querySelectorAll('[data-step]').length; const card=document.createElement('article'); card.className='rc49-step'; card.tabIndex=0; card.dataset.step=''; card.dataset.id=String(-(count+1)); card.dataset.name=`Nova etapa ${count+1}`; card.dataset.type='ANALISE'; card.dataset.initial=String(count===0); card.dataset.final='false'; card.innerHTML=`<div class="rc49-step__number">${count+1}</div><div><span>ANALISE</span><h2>Nova etapa ${count+1}</h2><p>Configure esta etapa no painel lateral.</p></div>`; canvas.append(card); select(card); });
  root.querySelector('[data-save]')?.addEventListener('click', async event => { const button=event.currentTarget; const steps=[...canvas.querySelectorAll('[data-step]')].map((card,index)=>({id:Number(card.dataset.id),nome:card.dataset.name,tipo:card.dataset.type,ordem:index+1,inicial:card.dataset.initial==='true',final:card.dataset.final==='true',permiteRetorno:true})); button.disabled=true; button.textContent='Salvando…'; try { const token=document.querySelector('input[name="__RequestVerificationToken"]')?.value; const response=await fetch(`/Workflows/Designer/${root.dataset.workflowId}`,{method:'POST',headers:{'Content-Type':'application/json','RequestVerificationToken':token},body:JSON.stringify({etapas:steps,transicoes:[]})}); if(!response.ok){const problem=await response.json(); throw new Error(problem.errors?.design?.join(' ')||'Não foi possível salvar.');} toast('Workflow salvo.'); } catch(error){toast(error.message,true);} finally{button.disabled=false;button.textContent='Salvar desenho';} });
})();
