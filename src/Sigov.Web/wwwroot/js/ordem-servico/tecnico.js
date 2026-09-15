import {api, key, toast} from './api.js';

const agenda = document.querySelector('[data-tecnico-agenda]');
const ordens = document.querySelector('[data-tecnico-ordens]');
const execucao = document.querySelector('[data-tecnico-execucao]');
const offline = document.querySelector('[data-offline]');
const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, character => ({'&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'}[character]));

function updateConnectivity() {
  offline?.classList.toggle('d-none', navigator.onLine);
  execucao?.querySelectorAll('button').forEach(button => { button.disabled = !navigator.onLine; });
}
window.addEventListener('online', updateConnectivity);
window.addEventListener('offline', updateConnectivity);
updateConnectivity();

function card(item) {
  const time = item.inicio ? new Date(item.inicio).toLocaleTimeString([], {hour: '2-digit', minute: '2-digit'}) : '';
  return `<article class="card"><div class="card-body"><div class="d-flex justify-content-between"><strong>${item.numero}</strong><span class="badge text-bg-primary">${item.status}</span></div><h2 class="h5">${item.cliente}</h2><p>${time} ${item.endereco ?? ''}</p><a class="btn btn-primary btn-lg w-100" href="/Tecnico/Ordens/${item.id}/Execucao">Abrir atendimento</a></div></article>`;
}

async function loadAgenda() {
  const start = new Date(); start.setHours(0, 0, 0, 0);
  const end = new Date(start); end.setDate(end.getDate() + 1);
  const items = await api(`/api/ordens-servico/agenda?inicio=${start.toISOString()}&fim=${end.toISOString()}`);
  agenda.querySelector('[data-items]').innerHTML = items.length ? items.map(card).join('') : '<div class="empty-state p-4 text-center">Nenhum atendimento agendado para hoje.</div>';
}

async function loadOrdens() {
  const page = await api('/api/ordens-servico?tamanho=50');
  ordens.querySelector('[data-items]').innerHTML = page.items.length ? page.items.map(card).join('') : '<p class="text-secondary">Nenhuma ordem atribuída.</p>';
}

let current;
async function loadExecution() {
  const id = execucao.dataset.id;
  current = await api(`/api/ordens-servico/${id}`);
  execucao.querySelector('[data-numero]').textContent = current.numero;
  execucao.querySelector('[data-status]').textContent = current.status;
  execucao.querySelector('[data-cliente]').textContent = current.cliente;
  execucao.querySelector('[data-descricao]').textContent = current.descricao;
  const checklist = await api(`/api/ordens-servico/${id}/checklist`);
  execucao.querySelector('[data-progress]').textContent = `${checklist.filter(item => item.respondido).length} de ${checklist.length} respondidos`;
  execucao.querySelector('[data-checklist]').innerHTML = checklist.length ? checklist.map(item => `<label class="card card-body"><span class="fw-semibold">${escapeHtml(item.titulo)}${item.obrigatorio ? ' <span class="text-danger">(obrigatório)</span>' : ''}</span><input class="form-control mt-2" name="${escapeHtml(item.id)}" value="${escapeHtml(item.resposta)}" data-version="${item.version}" maxlength="4000" ${item.obrigatorio ? 'required' : ''}><span class="form-text">Resposta salva individualmente. Deixe vazio para manter como pendente.</span></label>`).join('') : '<p class="text-secondary">Esta ordem não possui checklist.</p>';
  const history = await api(`/api/ordens-servico/${id}/historico`);
  execucao.querySelector('[data-history]').innerHTML = history.length ? history.map(item => `<li class="list-group-item"><strong>${escapeHtml(item.statusNovo)}</strong><span class="d-block small text-secondary">${new Date(item.criadoEm).toLocaleString()} · ${escapeHtml(item.criadoPor)}</span>${item.observacao ? `<span class="d-block">${escapeHtml(item.observacao)}</span>` : ''}</li>`).join('') : '<li class="list-group-item text-secondary">Nenhuma transição registrada.</li>';
}

async function transition(action) {
  const body = action === 'pausar' ? {motivo: 'Pausa operacional registrada pelo técnico', version: current.version} : action === 'retomar' ? {version: current.version} : action === 'concluir' ? {diagnostico: 'Registrado na execução', solucao: 'Serviço executado', version: current.version} : {inicioReal: new Date().toISOString(), latitude: null, longitude: null, version: current.version};
  await api(`/api/ordens-servico/${current.id}/${action}`, {method: 'POST', body: JSON.stringify(body)});
  toast('Ação registrada com sucesso.');
  await loadExecution();
}

if (agenda) loadAgenda().catch(error => toast(error.message, 'danger'));
if (ordens) loadOrdens().catch(error => toast(error.message, 'danger'));
if (execucao) {
  execucao.addEventListener('click', event => {
    const action = event.target.dataset.action;
    if (!action) return;
    event.target.disabled = true;
    transition(action).catch(error => toast(error.message, 'danger')).finally(() => { event.target.disabled = !navigator.onLine; });
  });
  execucao.querySelector('[data-checklist]').addEventListener('change', async event => {
    const input = event.target;
    const state = execucao.querySelector('[data-save-state]'); state.textContent = 'Salvando…';
    try { await api(`/api/ordens-servico/${execucao.dataset.id}/checklist/respostas`, {method: 'POST', body: JSON.stringify({itemId: input.name, resposta: input.value, observacao: null, evidenciaId: null, version: Number(input.dataset.version)})}); state.textContent = 'Progresso salvo e confirmado pelo servidor.'; toast('Progresso salvo. A ordem não foi concluída.'); await loadExecution(); } catch (error) { state.textContent = 'Não foi possível confirmar o salvamento. Consulte o estado persistido antes de repetir.'; toast(error.message, 'danger'); }
  });
  execucao.querySelector('[data-aceite]').addEventListener('submit', async event => {
    event.preventDefault(); const button = event.submitter; button.disabled = true;
    const values = Object.fromEntries(new FormData(event.currentTarget)); values.confirmado = values.confirmado === 'on'; values.evidenciaAssinaturaId = null;
    try { await api(`/api/ordens-servico/${execucao.dataset.id}/aceite`, {method: 'POST', headers: {'Idempotency-Key': key()}, body: JSON.stringify(values)}); toast('Aceite registrado.'); event.currentTarget.reset(); } catch (error) { toast(error.message, 'danger'); } finally { button.disabled = !navigator.onLine; }
  });
  loadExecution().catch(error => toast(error.message, 'danger'));
}
