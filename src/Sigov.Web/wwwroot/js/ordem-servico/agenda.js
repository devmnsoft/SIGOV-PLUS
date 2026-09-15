import { api, toast } from './api.js';

const root = document.querySelector('[data-os-agenda]');
const filter = root.querySelector('[data-filter]');
const agenda = root.querySelector('[data-agenda]');
const pending = root.querySelector('[data-pending]');
const error = root.querySelector('[data-error]');
const form = document.querySelector('[data-schedule]');
const modalElement = document.querySelector('#programarOs');
const modal = new bootstrap.Modal(modalElement);
let technicians = [];
let orders = new Map();

const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[char]);
const localInput = value => value ? new Date(value).toLocaleString('sv-SE').slice(0, 16).replace(' ', 'T') : '';
const formatPeriod = item => `${new Date(item.inicio).toLocaleString()} — ${new Date(item.fim).toLocaleString()}`;
const showError = message => { error.textContent = message; error.classList.remove('d-none'); error.focus(); };

function technicianOptions(selected) {
  return `<option value="">Selecione</option>${technicians.map(item => `<option value="${item.id}" ${item.id === selected ? 'selected' : ''}>${escapeHtml(item.nome)}</option>`).join('')}`;
}

function openSchedule(order, scheduled) {
  form.reset();
  form.elements.id.value = order.id;
  form.elements.version.value = order.version;
  form.elements.reprogramacao.value = scheduled ? 'true' : 'false';
  form.elements.tecnicoId.innerHTML = technicianOptions(order.tecnicoId);
  form.elements.inicio.value = localInput(order.agendadaInicio);
  form.elements.fim.value = localInput(order.agendadaFim);
  form.elements.local.value = order.endereco ?? '';
  form.querySelector('[data-order-label]').textContent = `${order.numero} — ${order.cliente}`;
  form.querySelector('[data-form-error]').classList.add('d-none');
  form.elements.observacao.required = scheduled;
  document.querySelector('#programar-title').textContent = scheduled ? 'Reprogramar ordem' : 'Programar ordem';
  modal.show();
}

async function load() {
  error.classList.add('d-none');
  pending.innerHTML = '<div class="p-3" role="status">Carregando ordens…</div>';
  agenda.innerHTML = '<tr><td colspan="5" class="p-4">Carregando programação…</td></tr>';
  const values = new FormData(filter);
  const start = new Date(`${values.get('inicio')}T00:00:00`);
  const end = new Date(`${values.get('fim')}T23:59:59.999`);
  const listQuery = new URLSearchParams({ pagina: '1', tamanho: '50', programada: 'false' });
  if (values.get('prioridade')) listQuery.set('prioridade', values.get('prioridade'));
  if (values.get('local')) listQuery.set('local', values.get('local'));
  const agendaQuery = new URLSearchParams({ inicio: start.toISOString(), fim: end.toISOString() });
  if (values.get('tecnicoId')) agendaQuery.set('tecnicoId', values.get('tecnicoId'));
  try {
    const [pendingResult, scheduled] = await Promise.all([api(`/api/ordens-servico?${listQuery}`), api(`/api/ordens-servico/agenda?${agendaQuery}`)]);
    const details = await Promise.all(scheduled.map(item => api(`/api/ordens-servico/${item.id}`)));
    orders = new Map([...pendingResult.items, ...details].map(item => [item.id, item]));
    pending.innerHTML = pendingResult.items.length ? pendingResult.items.map(item => `<article class="list-group-item"><div class="d-flex justify-content-between gap-2"><div><strong>${escapeHtml(item.numero)}</strong><div>${escapeHtml(item.cliente)}</div><small>${escapeHtml(item.prioridade)} · ${escapeHtml(item.status)} · ${escapeHtml(item.endereco || 'Local não informado')}</small></div><button class="btn btn-sm btn-primary align-self-start" data-programar="${item.id}">Programar</button></div></article>`).join('') : '<div class="p-4 text-center"><strong>Nenhuma ordem sem programação</strong><p class="mb-0">Os filtros não retornaram trabalho pendente.</p></div>';
    agenda.innerHTML = scheduled.length ? scheduled.map(item => `<tr><td><strong>${escapeHtml(item.numero)}</strong><div>${escapeHtml(item.cliente)}</div></td><td>${escapeHtml(formatPeriod(item))}</td><td>${escapeHtml(item.endereco || 'Não informado')}</td><td><span class="badge text-bg-primary">${escapeHtml(item.status)}</span></td><td><div class="d-flex gap-2"><a class="btn btn-sm btn-outline-secondary" href="/OrdemServico/Ordens/${item.id}?retorno=agenda">Abrir</a><button class="btn btn-sm btn-outline-primary" data-reprogramar="${item.id}">Reprogramar</button></div></td></tr>`).join('') : '<tr><td colspan="5" class="p-5 text-center"><strong>Nenhuma programação no período</strong><p class="mb-0">Altere os filtros ou programe uma ordem pendente.</p></td></tr>';
    root.querySelector('[data-total]').textContent = `${scheduled.length} registro(s)`;
  } catch (exception) { pending.innerHTML = ''; agenda.innerHTML = ''; showError(exception.message); }
}

root.addEventListener('click', event => {
  const id = event.target.dataset.programar ?? event.target.dataset.reprogramar;
  if (id && orders.has(id)) openSchedule(orders.get(id), Boolean(event.target.dataset.reprogramar));
});
filter.addEventListener('submit', event => { event.preventDefault(); load(); });
root.querySelector('[data-today]').addEventListener('click', () => { const today = new Date().toISOString().slice(0, 10); filter.elements.inicio.value = today; filter.elements.fim.value = today; load(); });
form.addEventListener('submit', async event => {
  event.preventDefault();
  const formError = form.querySelector('[data-form-error]');
  formError.classList.add('d-none');
  if (!form.reportValidity()) return;
  const data = Object.fromEntries(new FormData(form));
  if (new Date(data.fim) <= new Date(data.inicio)) { formError.textContent = 'O término previsto deve ser posterior ao início previsto.'; formError.classList.remove('d-none'); return; }
  if (!window.confirm(`Confirma a programação de ${form.querySelector('[data-order-label]').textContent}?`)) return;
  event.submitter.disabled = true;
  try {
    await api(`/api/ordens-servico/${data.id}/agendar`, { method: 'POST', body: JSON.stringify({ tecnicoId: data.tecnicoId, equipeId: null, inicio: new Date(data.inicio).toISOString(), fim: new Date(data.fim).toISOString(), janela: null, observacao: data.observacao || null, autorizarConflito: data.autorizarConflito === 'on', justificativa: data.justificativa || null, version: Number(data.version) }) });
    modal.hide(); toast(data.reprogramacao === 'true' ? 'Ordem reprogramada e histórico preservado.' : 'Ordem programada.'); await load();
  } catch (exception) {
    formError.textContent = exception.name === 'AbortError' ? 'Resultado desconhecido. Consulte a programação antes de repetir.' : exception.message;
    formError.classList.remove('d-none');
    if (/sobreposição|conflito/i.test(exception.message)) form.querySelector('[data-conflict-fields]').classList.remove('d-none');
  } finally { event.submitter.disabled = false; }
});
modalElement.addEventListener('hidden.bs.modal', () => form.querySelector('[data-conflict-fields]').classList.add('d-none'));

const today = new Date();
filter.elements.inicio.value = today.toISOString().slice(0, 10);
const week = new Date(today); week.setDate(week.getDate() + 7); filter.elements.fim.value = week.toISOString().slice(0, 10);
try { technicians = await api('/api/ordens-servico/tecnicos'); filter.elements.tecnicoId.innerHTML += technicians.map(item => `<option value="${item.id}">${escapeHtml(item.nome)}</option>`).join(''); await load(); }
catch (exception) { showError(exception.message); }
