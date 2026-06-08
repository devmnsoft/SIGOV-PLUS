$(function () {
  const api = window.sigovApi;
  const ui = window.sigovUi;
  function badge(v) { return `<span class="badge bg-secondary">${v || ''}</span>`; }
  function formObject($form) { const o = {}; $form.serializeArray().forEach(x => { o[x.name] = x.value; }); return o; }
  function handleError(e) { ui.notify(e.message && e.message.includes('403') ? 'Acesso negado.' : (e.message || 'Erro inesperado.'), 'danger'); }
  async function carregar() {
    const data = await api.request('/api/processos?' + $('#processos-filtro').serialize());
    const rows = (data.data.items || []).map(p => `<tr><td>${p.numero}</td><td>${p.assunto}</td><td>${p.tipoProcesso}</td><td>${p.interessado || '-'}</td><td>${badge(p.status)}</td><td>${badge(p.prioridade)}</td><td>${new Date(p.dataAbertura).toLocaleDateString()}</td><td>${p.prazoRespostaAt ? new Date(p.prazoRespostaAt).toLocaleDateString() : '-'}</td><td><a class="btn btn-sm btn-outline-primary" href="/Processos/Detalhe/${p.id}">Ver</a> <button class="btn btn-sm btn-outline-secondary js-mov" data-id="${p.id}">Movimentar</button> <button class="btn btn-sm btn-outline-info js-par" data-id="${p.id}">Parecer</button></td></tr>`).join('');
    $('#processos-table tbody').html(rows || '<tr><td colspan="9" class="text-muted">Nenhum processo encontrado.</td></tr>');
  }
  $('#processos-filtro').on('submit', function (e) { e.preventDefault(); carregar().catch(handleError); });
  $('#processo-form').on('submit', async function (e) { e.preventDefault(); if (!$(this).valid()) return; const f = formObject($(this)); const body = { tipoProcessoId: Number(f.TipoProcessoId), assunto: f.Assunto, descricao: f.Descricao, interessadoPessoaId: f.InteressadoPessoaId ? Number(f.InteressadoPessoaId) : null, unidadeOrigemId: f.UnidadeOrigemId ? Number(f.UnidadeOrigemId) : null, unidadeAtualId: f.UnidadeAtualId ? Number(f.UnidadeAtualId) : null, prioridade: f.Prioridade, sigiloso: !!f.Sigiloso, prazoRespostaAt: f.PrazoRespostaAt || null, observacao: f.Observacao }; try { await api.request('/api/processos', { method: 'POST', body: JSON.stringify(body) }); ui.notify('Processo salvo com sucesso.', 'success'); } catch (e) { handleError(e); } });
  $(document).on('click', '.js-mov', function () { $('#ProcessoId').val($(this).data('id')); new bootstrap.Modal('#modalMovimentar').show(); });
  $(document).on('click', '.js-par', function () { $('#ProcessoId').val($(this).data('id')); new bootstrap.Modal('#modalParecer').show(); });
  $('#movimentar-form').on('submit', async function (e) { e.preventDefault(); const f = formObject($(this)); try { await api.request(`/api/processos/${f.ProcessoId}/movimentar`, { method: 'POST', body: JSON.stringify({ unidadeDestinoId: f.UnidadeDestinoId ? Number(f.UnidadeDestinoId) : null, usuarioDestinoId: f.UsuarioDestinoId ? Number(f.UsuarioDestinoId) : null, despacho: f.Despacho, statusNovo: f.StatusNovo || null }) }); ui.notify('Processo movimentado.', 'success'); } catch (e) { handleError(e); } });
  $('#parecer-form').on('submit', async function (e) { e.preventDefault(); const f = formObject($(this)); try { await api.request(`/api/processos/${f.ProcessoId}/pareceres`, { method: 'POST', body: JSON.stringify({ titulo: f.Titulo, texto: f.Texto, tipoParecer: f.TipoParecer || 'TECNICO', sigiloso: !!f.Sigiloso }) }); ui.notify('Parecer emitido.', 'success'); } catch (e) { handleError(e); } });
  const detalheId = $('#processo-id').val();
  if (detalheId) api.request(`/api/processos/${detalheId}`).then(r => { const p = r.data; $('#processo-numero').text(p.numero); $('#dados').html(`<h2>${p.assunto}</h2><p>${p.descricao || ''}</p><p>Status: ${badge(p.status)} Prioridade: ${badge(p.prioridade)}</p>`); $('#processo-timeline').html((p.movimentacoes || []).map(m => `<div class="list-group-item"><strong>${new Date(m.movimentadoAt).toLocaleString()}</strong><br>${m.despacho}</div>`).join('') || '<div class="list-group-item text-muted">Sem movimentações.</div>'); }).catch(handleError);
});
