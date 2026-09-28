import { request, lock } from './compras-api.js';

const button = document.querySelector('[data-enviar]');
button?.addEventListener('click', async () => {
  if (!window.confirm('Enviar esta requisição para aprovação? Após o envio, o rascunho não poderá ser alterado diretamente.')) return;
  const feedback = document.querySelector('.form-feedback');
  const storageKey = `envio:${button.dataset.id}`;
  const chaveEnvio = sessionStorage.getItem(storageKey) ?? crypto.randomUUID();
  sessionStorage.setItem(storageKey, chaveEnvio);
  lock(button, true);
  try {
    await request(`/api/compras-empresariais/requisicoes/${button.dataset.id}/enviar?version=${button.dataset.version}`, { method: 'POST', headers: { 'Idempotency-Key': chaveEnvio } });
    feedback.textContent = 'Requisição enviada para aprovação com sucesso.';
    window.location.reload();
  } catch (error) {
    feedback.textContent = error.message;
    feedback.classList.add('is-error');
  } finally {
    lock(button, false);
  }
});
