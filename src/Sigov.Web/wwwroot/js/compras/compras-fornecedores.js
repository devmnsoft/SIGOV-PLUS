import { request, lock } from './compras-api.js';
const form = document.querySelector('#fornecedor-form');
form?.addEventListener('submit', async event => {
  event.preventDefault(); const button = form.querySelector('button[type="submit"]'); const feedback = form.querySelector('.form-feedback'); lock(button, true);
  const data = Object.fromEntries(new FormData(form));
  const payload = { ...data, prazoMedio: 0, observacoes: null, porte: null };
  try { const result = await request('/api/compras-empresariais/fornecedores', { method: 'POST', body: JSON.stringify(payload) }); window.location.assign(`/ComprasEmpresariais/Fornecedores/${result.id}`); }
  catch (error) { feedback.textContent = error.message; feedback.classList.add('is-error'); }
  finally { lock(button, false); }
});
