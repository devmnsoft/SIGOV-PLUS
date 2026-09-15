import { request, lock } from './compras-api.js';
const form=document.querySelector('#requisicao-form'), host=form?.querySelector('[data-items]'), template=document.querySelector('#item-template');
let dirty=false;
// Preserve a chave após falha/timeout: repetir a mesma intenção não deve criar outro rascunho.
const operationKey=crypto.randomUUID();
function add(item){const row=template.content.firstElementChild.cloneNode(true);row.querySelector('[data-remove]').addEventListener('click',()=>{row.remove();dirty=true;});if(item){for(const field of ['tipo','descricao','unidade','quantidade','valorEstimado'])row.querySelector(`[data-field="${field}"]`).value=item[field]??'';}host.append(row);}
form?.querySelector('[data-add-item]')?.addEventListener('click',()=>add());
if(form){const initial=document.querySelector('#requisicao-items');const items=initial?JSON.parse(initial.textContent):[];if(items.length)items.forEach(add);else add();}
form?.addEventListener('input',()=>{dirty=true;});
window.addEventListener('beforeunload',event=>{if(dirty){event.preventDefault();event.returnValue='';}});
form?.addEventListener('submit',async event=>{
  event.preventDefault();
  const button=form.querySelector('button[type="submit"]'),feedback=form.querySelector('.form-feedback'),summary=form.querySelector('[data-validation-summary]');
  feedback.textContent='';feedback.classList.remove('is-error');summary.hidden=true;summary.textContent='';
  if(!form.checkValidity()){
    const invalid=[...form.querySelectorAll(':invalid')];
    invalid.forEach(field=>field.setAttribute('aria-invalid','true'));
    summary.textContent='Revise os campos obrigatórios e os valores informados.';summary.hidden=false;summary.focus();
    invalid[0]?.focus();return;
  }
  form.querySelectorAll('[aria-invalid="true"]').forEach(field=>field.removeAttribute('aria-invalid'));
  const values=Object.fromEntries(new FormData(form));
  const items=[...host.querySelectorAll('.item-row')].map(row=>({tipo:row.querySelector('[data-field="tipo"]').value,descricao:row.querySelector('[data-field="descricao"]').value.trim(),especificacao:null,unidade:row.querySelector('[data-field="unidade"]').value.trim().toUpperCase(),quantidade:Number(row.querySelector('[data-field="quantidade"]').value),valorEstimado:Number(row.querySelector('[data-field="valorEstimado"]').value||0),permiteParcial:true,exigeInspecao:false}));
  const unique=new Set(items.map(item=>`${item.tipo}|${item.descricao.toLocaleUpperCase()}|${item.unidade}`));
  if(unique.size!==items.length){feedback.textContent='Consolide os itens repetidos antes de salvar.';feedback.classList.add('is-error');feedback.focus();return;}
  const editing=Boolean(form.dataset.id);
  const payload={setor:values.setor,urgencia:values.urgencia,justificativa:values.justificativa,dataNecessaria:values.dataNecessaria||null,observacoes:null,centroCustoId:null,projetoId:null,contratoId:null,ordemServicoId:null,almoxarifadoId:null,itens:items,...(editing?{version:Number(form.dataset.version)}:{})};
  lock(button,true);
  try{const result=await request(editing?`/api/compras-empresariais/requisicoes/${form.dataset.id}`:'/api/compras-empresariais/requisicoes',{method:editing?'PUT':'POST',headers:{'Idempotency-Key':operationKey},body:JSON.stringify(payload)});dirty=false;window.location.assign(`/ComprasEmpresariais/Requisicoes/${editing?form.dataset.id:result.id}`);}
  catch(error){feedback.textContent=error.message;feedback.classList.add('is-error');feedback.focus();}
  finally{lock(button,false);}
});
