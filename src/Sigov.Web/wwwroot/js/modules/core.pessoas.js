$(function () {
  'use strict';
  const api = window.sigovApi;
  const ui = window.sigovUi || { notify: function (message) { if (window.Sigov && window.Sigov.toast) { window.Sigov.toast.show(message, 'info'); } } };

  function formObject($form) {
    const data = {};
    $form.serializeArray().forEach((item) => { data[item.name] = item.value; });
    data.Ativo = $form.find('[name="Ativo"]').is(':checked') || data.Ativo === 'true' || data.Ativo === 'on';
    return data;
  }

  function maskDocument(value) {
    const digits = (value || '').replace(/\D/g, '');
    if (digits.length === 11) return `${digits.substring(0, 3)}.***.***-${digits.substring(9)}`;
    if (digits.length === 14) return `${digits.substring(0, 2)}.***.***/****-${digits.substring(12)}`;
    return value ? '***' : '-';
  }

  function errorMessage(error) {
    if (error && error.status && window.Sigov && window.Sigov.errorMapper) return window.Sigov.errorMapper.message(error.status);
    return (error && error.message) || 'Não foi possível concluir a operação.';
  }

  function notify(message, type) { ui.notify(message, type || 'info'); }

  function endpoint(path) { return api ? api.request(path) : Promise.reject(new Error('API indisponível nesta sessão.')); }

  function request(path, options) { return api ? api.request(path, options || {}) : Promise.reject(new Error('API indisponível nesta sessão.')); }

  function enderecoFromForm(data) {
    if (!data.Logradouro && !data.Municipio && !data.Uf) return null;
    return { logradouro: data.Logradouro || '', numero: data.Numero || null, complemento: data.Complemento || null, bairro: data.Bairro || null, municipio: data.Municipio || '', uf: data.Uf || '', cep: data.Cep || null, observacao: null };
  }

  function pessoaPayload(data, includeEndereco) {
    const endereco = includeEndereco ? enderecoFromForm(data) : null;
    return { tipoPessoa: data.TipoPessoa, nome: data.Nome, nomeSocial: data.NomeSocial || null, documento: data.Documento || null, email: data.Email || null, telefone: data.Telefone || null, classificacaoLgpd: data.ClassificacaoLgpd || 'Pessoal', observacao: data.Observacao || null, ativo: !!data.Ativo, enderecos: endereco ? [endereco] : [] };
  }

  function renderEndereco(enderecos) {
    const item = (enderecos || [])[0];
    if (!item) return '<span class="text-muted">Sem endereço</span>';
    return `${item.logradouro || item.Logradouro}, ${item.numero || item.Numero || 's/n'} - ${item.municipio || item.Municipio}/${item.uf || item.Uf}`;
  }

  $('#pessoas-filtro').on('submit', async function (event) {
    event.preventDefault();
    $('#pessoas-table tbody').html('<tr><td colspan="7" class="text-center text-muted p-4">Carregando...</td></tr>');
    try {
      const response = await endpoint(`/api/pessoas?${$(this).serialize()}&page=1&pageSize=20`);
      const rows = ((response.data && response.data.items) || []).map((item) => `<tr>
        <td><strong>${item.nome || '-'}</strong><br><small class="text-muted">tenant_id isolado</small></td>
        <td>${item.tipoPessoa === 'J' ? 'Jurídica' : 'Física'}</td>
        <td><span class="sigov-masked" title="Requer permissão para dados completos">${maskDocument(item.documento)}</span></td>
        <td><span class="badge text-bg-info">${item.classificacaoLgpd || 'Pessoal'}</span></td>
        <td>${renderEndereco(item.enderecos)}</td>
        <td><span class="badge text-bg-${item.ativo ? 'success' : 'secondary'}">${item.ativo ? 'Ativo' : 'Inativo'}</span></td>
        <td class="text-end"><div class="btn-group btn-group-sm"><a class="btn btn-outline-primary" href="/Pessoas/Detalhe/${item.id}">Detalhar</a><a class="btn btn-outline-secondary" href="/Pessoas/Editar/${item.id}" data-sigov-permission="core.pessoas.editar">Editar</a><button class="btn btn-outline-danger" data-sigov-action="delete-pessoa" data-id="${item.id}" data-sigov-permission="core.pessoas.excluir">Excluir</button><a class="btn btn-outline-dark" href="/Auditoria/Timeline?chave=sigov.pessoa:${item.id}">Auditoria</a></div></td>
      </tr>`).join('');
      $('#pessoas-table tbody').html(rows || '<tr><td colspan="7" class="text-center text-muted p-4">Nenhuma pessoa encontrada. Ajuste os filtros ou cadastre uma nova pessoa.</td></tr>');
    } catch (error) {
      $('#pessoas-table tbody').html('<tr><td colspan="7" class="text-center text-danger p-4">' + errorMessage(error) + '</td></tr>');
      notify(errorMessage(error), 'danger');
    }
  });

  $('#pessoa-form').on('submit', async function (event) {
    event.preventDefault();
    const $form = $(this);
    if ($.validator && !$form.valid()) return;
    const data = formObject($form);
    const id = $('#pessoa-id').val();
    try {
      if (id) {
        await request(`/api/pessoas/${id}`, { method: 'PUT', body: JSON.stringify(pessoaPayload(data, false)), headers: { RequestVerificationToken: $form.find('[name="__RequestVerificationToken"]').val() || '' } });
        notify('Pessoa atualizada com sucesso.', 'success');
      } else {
        const response = await request('/api/pessoas', { method: 'POST', body: JSON.stringify(pessoaPayload(data, true)), headers: { RequestVerificationToken: $form.find('[name="__RequestVerificationToken"]').val() || '' } });
        notify('Pessoa criada com sucesso.', 'success');
        if (response.data) window.location.href = `/Pessoas/Detalhe/${response.data}`;
      }
    } catch (error) { notify(errorMessage(error), 'danger'); }
  });

  async function carregarDetalhe() {
    const id = $('#pessoa-id').val();
    if (!id) return;
    try {
      const response = await endpoint(`/api/pessoas/${id}`);
      const item = response.data || {};
      $('#Nome').val(item.nome || ''); $('#NomeSocial').val(item.nomeSocial || ''); $('#Documento').val(item.documento || ''); $('#TipoPessoa').val(item.tipoPessoa || 'F'); $('#Observacao').val(item.observacao || ''); $('#Ativo').prop('checked', item.ativo !== false);
      $('#pessoa-detalhe').html(`<h2 class="h4">${item.nome || 'Pessoa'}</h2><p><strong>Tipo:</strong> ${item.tipoPessoa === 'J' ? 'Jurídica' : 'Física'} • <strong>Documento:</strong> ${maskDocument(item.documento)}</p><p><strong>LGPD:</strong> ${item.classificacaoLgpd || 'Pessoal'} • <strong>Status:</strong> ${item.ativo !== false ? 'Ativo' : 'Inativo'}</p><h3 class="h5">Endereços</h3><ul>${(item.enderecos || []).map((e) => `<li>${e.logradouro}, ${e.numero || 's/n'} - ${e.bairro || ''} ${e.municipio}/${e.uf} CEP ${e.cep || '-'}</li>`).join('') || '<li>Nenhum endereço cadastrado.</li>'}</ul>`);
    } catch (error) { $('#pessoa-detalhe').html('<div class="text-danger">' + errorMessage(error) + '</div>'); notify(errorMessage(error), 'danger'); }
  }

  $('#endereco-form').on('submit', async function (event) {
    event.preventDefault();
    const id = $('#pessoa-id').val();
    const $form = $(this);
    try {
      await request(`/api/pessoas/${id}/enderecos`, { method: 'POST', body: JSON.stringify(enderecoFromForm(formObject($form))), headers: { RequestVerificationToken: $form.find('[name="__RequestVerificationToken"]').val() || '' } });
      notify('Endereço adicionado.', 'success');
      await carregarDetalhe();
      this.reset();
    } catch (error) { notify(errorMessage(error), 'danger'); }
  });

  $(document).on('click', '[data-sigov-action="delete-pessoa"]', async function () {
    const id = $(this).data('id');
    if (!window.SigovConfirm || !await window.SigovConfirm.show({ title: 'Excluir pessoa', message: 'Confirma a exclusão lógica da pessoa?', variant: 'danger', confirmText: 'Excluir' })) return;
    try { await request(`/api/pessoas/${id}`, { method: 'DELETE' }); notify('Pessoa excluída com soft delete.', 'success'); $('#pessoas-filtro').trigger('submit'); } catch (error) { notify(errorMessage(error), 'danger'); }
  });

  carregarDetalhe();
  if ($('#pessoas-filtro').length) $('#pessoas-filtro').trigger('submit');
});
