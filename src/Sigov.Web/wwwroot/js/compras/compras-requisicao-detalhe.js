import { request, lock } from './compras-api.js';

const button = document.querySelector('[data-enviar]');
button?.addEventListener('click', async () => {
  const feedback = document.querySelector('.form-feedback');
  lock(button, true);
  try {
    await request(`/api/compras-empresariais/requisicoes/${button.dataset.id}/enviar?version=${button.dataset.version}`, { method: 'POST' });
    feedback.textContent = 'Requisição enviada para aprovação com sucesso.';
    window.location.reload();
  } catch (error) {
    feedback.textContent = error.message;
    feedback.classList.add('is-error');
  } finally {
    lock(button, false);
  }
});
