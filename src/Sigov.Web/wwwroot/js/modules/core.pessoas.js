$(function () {
  const api = window.sigovApi;
  const ui = window.sigovUi;

  function formObject($form) {
    const data = {};
    $form.serializeArray().forEach((item) => { data[item.name] = item.value; });
    return data;
  }

  function enderecoFromForm(data) {
    if (!data.Logradouro && !data.Municipio && !data.Uf) return null;
    return {
      logradouro: data.Logradouro,
      numero: data.Numero || null,
      complemento: data.Complemento || null,
      bairro: data.Bairro || null,
      municipio: data.Municipio,
      uf: data.Uf,
      cep: data.Cep || null,
      observacao: null
    };
  }

  function pessoaPayload(data, includeEndereco) {
    const endereco = includeEndereco ? enderecoFromForm(data) : null;
    return {
      tipoPessoa: data.TipoPessoa,
      nome: data.Nome,
      nomeSocial: data.NomeSocial || null,
      documento: data.Documento || null,
      observacao: data.Observacao || null,
      ativo: data.Ativo === 'true' || data.Ativo === 'on',
      enderecos: endereco ? [endereco] : []
    };
  }

  function handleError(error) {
    ui.notify(error.message || 'Erro ao processar pessoa.', 'danger');
  }

  function renderEndereco(enderecos) {
    const item = (enderecos || [])[0];
    if (!item) return '-';
    return `${item.logradouro || item.Logradouro}, ${item.numero || item.Numero || 's/n'} - ${item.municipio || item.Municipio}/${item.uf || item.Uf}`;
  }

  $('#pessoas-filtro').on('submit', async function (event) {
    event.preventDefault();
    try {
      const response = await api.request(`/api/pessoas?${$(this).serialize()}`);
      const rows = (response.data.items || []).map((item) => `<tr>
        <td>${item.nome}</td>
        <td>${item.tipoPessoa === 'J' ? 'Jurídica' : 'Física'}</td>
        <td>${item.documento || '-'}</td>
        <td>${renderEndereco(item.enderecos)}</td>
        <td><span class="badge bg-${item.ativo ? 'success' : 'secondary'}">${item.ativo ? 'Ativo' : 'Inativo'}</span></td>
        <td class="text-end"><a class="btn btn-sm btn-outline-primary" href="/Pessoas/Detalhe/${item.id}">Ver</a></td>
      </tr>`).join('');
      $('#pessoas-table tbody').html(rows || '<tr><td colspan="6" class="text-center text-muted">Nenhuma pessoa encontrada.</td></tr>');
    } catch (error) {
      handleError(error);
    }
  });

  $('#pessoa-form').on('submit', async function (event) {
    event.preventDefault();
    const data = formObject($(this));
    const id = $('#pessoa-id').val();
    try {
      if (id) {
        await api.request(`/api/pessoas/${id}`, { method: 'PUT', body: JSON.stringify(pessoaPayload(data, false)) });
        ui.notify('Pessoa atualizada com sucesso.', 'success');
      } else {
        const response = await api.request('/api/pessoas', { method: 'POST', body: JSON.stringify(pessoaPayload(data, true)) });
        ui.notify('Pessoa criada com sucesso.', 'success');
        if (response.data) window.location.href = `/Pessoas/Detalhe/${response.data}`;
      }
    } catch (error) {
      handleError(error);
    }
  });

  async function carregarDetalhe() {
    const id = $('#pessoa-id').val();
    if (!id) return;
    try {
      const response = await api.request(`/api/pessoas/${id}`);
      const item = response.data;
      $('#Nome').val(item.nome);
      $('#NomeSocial').val(item.nomeSocial || '');
      $('#Documento').val(item.documento || '');
      $('#TipoPessoa').val(item.tipoPessoa);
      $('#Observacao').val(item.observacao || '');
      $('#Ativo').prop('checked', item.ativo);
      $('#pessoa-detalhe').html(`<h2 class="h4">${item.nome}</h2>
        <p><strong>Tipo:</strong> ${item.tipoPessoa === 'J' ? 'Jurídica' : 'Física'} • <strong>Documento:</strong> ${item.documento || '-'}</p>
        <p><strong>LGPD:</strong> ${item.classificacaoLgpd} • <strong>Status:</strong> ${item.ativo ? 'Ativo' : 'Inativo'}</p>
        <h3 class="h5">Endereços</h3>
        <ul>${(item.enderecos || []).map((e) => `<li>${e.logradouro}, ${e.numero || 's/n'} - ${e.bairro || ''} ${e.municipio}/${e.uf} CEP ${e.cep || '-'}</li>`).join('') || '<li>Nenhum endereço cadastrado.</li>'}</ul>`);
    } catch (error) {
      handleError(error);
    }
  }

  $('#endereco-form').on('submit', async function (event) {
    event.preventDefault();
    const id = $('#pessoa-id').val();
    try {
      await api.request(`/api/pessoas/${id}/enderecos`, { method: 'POST', body: JSON.stringify(enderecoFromForm(formObject($(this)))) });
      ui.notify('Endereço adicionado.', 'success');
      await carregarDetalhe();
      this.reset();
    } catch (error) {
      handleError(error);
    }
  });

  carregarDetalhe();
  if ($('#pessoas-filtro').length) $('#pessoas-filtro').trigger('submit');
});
