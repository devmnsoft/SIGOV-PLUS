import { request, lock } from './compras-api.js';

const BASE = '/api/compras-empresariais';
const WEB = '/ComprasEmpresariais';

function feedback(raiz, mensagem, comErro = false) {
  const elemento = raiz.querySelector('.form-feedback');
  if (!elemento) return;
  elemento.textContent = mensagem || '';
  elemento.classList.toggle('is-error', comErro);
  if (mensagem) elemento.focus();
}

function chaveSessao(nome) {
  const atual = sessionStorage.getItem(nome);
  if (atual) return atual;
  const nova = crypto.randomUUID();
  sessionStorage.setItem(nome, nova);
  return nova;
}

function paraInputLocal(data) {
  const p = (n) => String(n).padStart(2, '0');
  return `${data.getFullYear()}-${p(data.getMonth() + 1)}-${p(data.getDate())}T${p(data.getHours())}:${p(data.getMinutes())}`;
}

/* ------------------------------------------------------------------ */
/* Nova cotação                                                        */
/* ------------------------------------------------------------------ */
const nova = document.querySelector('[data-cotacao-nova]');
if (nova) iniciarNova(nova);

async function iniciarNova(raiz) {
  const statusProdutos = raiz.querySelector('[data-produtos-status]');
  let produtos = [];
  try {
    const dados = await request(`${BASE}/cotacoes/produtos?busca=&limite=50`);
    produtos = Array.isArray(dados) ? dados : [];
    raiz.querySelectorAll('select[data-produto]').forEach((select) => {
      produtos.forEach((produto) => {
        const opcao = document.createElement('option');
        opcao.value = produto.id;
        opcao.textContent = `${produto.sku} — ${produto.nome} (${produto.unidade})`;
        select.appendChild(opcao);
      });
    });
    if (statusProdutos) statusProdutos.textContent = `${produtos.length} produto(s) carregado(s) do catálogo.`;
  } catch (erro) {
    if (statusProdutos) {
      statusProdutos.textContent = `Não foi possível carregar o catálogo de produtos: ${erro.message}`;
      statusProdutos.classList.add('text-danger');
    }
  }

  raiz.querySelector('[data-busca-produto]')?.addEventListener('input', (evento) => {
    const termo = evento.target.value.trim().toLowerCase();
    raiz.querySelectorAll('select[data-produto]').forEach((select) => {
      [...select.options].forEach((opcao) => {
        if (opcao.value === '') return;
        opcao.hidden = !opcao.textContent.toLowerCase().includes(termo);
      });
    });
  });

  const fornecedoresBox = raiz.querySelector('[data-fornecedores]');
  try {
    const pagina = await request(`${BASE}/fornecedores?pagina=1&tamanho=100`);
    const itens = pagina?.items ?? [];
    fornecedoresBox.innerHTML = '';
    if (itens.length === 0) {
      fornecedoresBox.innerHTML = '<p class="text-muted mb-0">Nenhum fornecedor cadastrado disponível.</p>';
    }
    itens.forEach((fornecedor, indice) => {
      const indisponivel = fornecedor.status === 'BLOQUEADO' || fornecedor.status === 'SUSPENSO';
      const idCheck = `fornecedor-check-${indice}`;
      const rotulo = `${fornecedor.razaoSocial}${fornecedor.nomeFantasia ? ` · ${fornecedor.nomeFantasia}` : ''} — ${fornecedor.documentoMascarado}`;
      const check = document.createElement('input');
      check.type = 'checkbox';
      check.className = 'form-check-input mt-1';
      check.id = idCheck;
      check.name = 'fornecedores-convidados';
      check.value = fornecedor.id;
      check.setAttribute('data-fornecedor-id', '');
      if (indisponivel) check.disabled = true;
      const texto = document.createElement('span');
      texto.className = 'form-check-label small';
      texto.textContent = `${rotulo}${indisponivel ? ` (situação ${fornecedor.status.replace('_', ' ')} — indisponível para convite)` : ''}`;
      const linha = document.createElement('label');
      linha.className = 'col-md-6 col-lg-4 d-flex align-items-start gap-1';
      linha.append(check, texto);
      fornecedoresBox.appendChild(linha);
    });
  } catch (erro) {
    fornecedoresBox.innerHTML = `<p class="text-danger mb-0">Não foi possível carregar os fornecedores: ${erro.message}</p>`;
  }

  const prazo = raiz.querySelector('[data-prazo]');
  if (prazo) {
    prazo.min = paraInputLocal(new Date(Date.now() + 3_600_000));
    prazo.max = paraInputLocal(new Date(Date.now() + 90 * 86_400_000));
  }

  raiz.querySelector('[data-requisicao-seletor]')?.addEventListener('change', (evento) => {
    window.location.href = evento.target.value
      ? `${WEB}/Cotacoes/Nova?requisicaoId=${evento.target.value}`
      : `${WEB}/Cotacoes/Nova`;
  });

  raiz.querySelector('[data-enviar-cotacao]')?.addEventListener('click', async (evento) => {
    const botao = evento.currentTarget;
    const elaboracao = raiz.querySelector('[data-elaboracao]');
    if (!elaboracao) { feedback(raiz, 'Selecione uma requisição aprovada para montar a rodada.', true); return; }
    const erros = [];
    const caixas = [...raiz.querySelectorAll('[data-item-selecao]:not(:disabled)')];
    const selecionadas = caixas.filter((caixa) => caixa.checked);
    if (selecionadas.length === 0) erros.push('Marque ao menos um item da requisição para incluir na rodada.');
    const linhasProduto = [];
    for (const caixa of selecionadas) {
      const linha = caixa.closest('tr');
      const select = linha?.querySelector('select[data-produto]');
      const rotulo = linha ? linha.cells[1].textContent.replace(/\s+/g, ' ').trim().slice(0, 48) : 'item';
      if (!select?.value) { erros.push(`Vincule um produto do catálogo ao item “${rotulo}”.`); continue; }
      linhasProduto.push({ requisicaoItemId: caixa.dataset.requisicaoItemId, produtoId: select.value });
    }
    const convidados = [...raiz.querySelectorAll('[data-fornecedor-id]:checked')].map((caixa) => caixa.value);
    if (convidados.length < 1) erros.push('Convide ao menos um fornecedor.');
    if (convidados.length > 20) erros.push('Convide no máximo 20 fornecedores.');
    if (!prazo?.value) erros.push('Informe o prazo limite das propostas.');
    else if (Number.isNaN(Date.parse(prazo.value)) || Date.parse(prazo.value) < Date.parse(prazo.min) || Date.parse(prazo.value) > Date.parse(prazo.max)) {
      erros.push('O prazo precisa estar entre 1 hora e 90 dias a partir de agora.');
    }
    if (erros.length > 0) { feedback(raiz, erros.join(' '), true); return; }
    lock(botao, true);
    try {
      const resposta = await request(`${BASE}/cotacoes`, {
        method: 'POST',
        headers: { 'Idempotency-Key': chaveSessao(`cotacao:criar:${elaboracao.dataset.requisicaoId}`) },
        body: JSON.stringify({
          requisicaoId: elaboracao.dataset.requisicaoId,
          fornecedorIds: convidados,
          prazo: prazo.value,
          produtos: linhasProduto,
        }),
      });
      window.location.href = `${WEB}/Cotacoes/${resposta.id}`;
    } catch (erro) {
      feedback(raiz, erro.message, true);
    } finally {
      lock(botao, false);
    }
  });
}

/* ------------------------------------------------------------------ */
/* Detalhe da cotação                                                  */
/* ------------------------------------------------------------------ */
const detalhe = document.querySelector('[data-cotacao-detalhe]');
if (detalhe) iniciarDetalhe(detalhe);

function iniciarDetalhe(raiz) {
  const cotacaoId = raiz.dataset.id;
  const versao = Number(raiz.dataset.version);
  const alvo = raiz.querySelector('[data-resposta-destaque]');
  let itens = [];
  try {
    itens = JSON.parse(document.getElementById('cotacao-itens-json')?.textContent || '[]');
  } catch { itens = []; }

  raiz.querySelectorAll('[data-registrar-proposta]').forEach((botao) => {
    botao.addEventListener('click', () => montarFormResposta(botao));
  });

  function montarFormResposta(botao) {
    const conviteId = botao.dataset.conviteId;
    const conviteVersion = Number(botao.dataset.conviteVersion);
    const fornecedorNome = botao.dataset.fornecedor;
    alvo.innerHTML = '';

    const secao = document.createElement('section');
    secao.className = 'panel mt-3';
    secao.setAttribute('aria-label', `Proposta de ${fornecedorNome}`);
    const titulo = document.createElement('h3');
    titulo.className = 'h6';
    titulo.textContent = `Proposta de ${fornecedorNome}`;
    secao.appendChild(titulo);

    const wrapper = document.createElement('div');
    wrapper.className = 'table-responsive';
    const tabela = document.createElement('table');
    tabela.className = 'table align-middle';
    const thead = document.createElement('thead');
    thead.innerHTML = '<tr><th scope="col">Item</th><th scope="col">Preço unitário</th><th scope="col">Desconto %</th><th scope="col">Imposto %</th><th scope="col">Frete</th><th scope="col">Prazo (dias)</th><th scope="col">Marca</th><th scope="col">Fabricante</th><th scope="col"><span class="visually-hidden">Proposta recusada</span></th></tr>';
    tabela.appendChild(thead);
    const tbody = document.createElement('tbody');
    itens.forEach((item) => {
      const tr = document.createElement('tr');
      tr.setAttribute('data-resposta-item', '');
      const celulaItem = document.createElement('td');
      celulaItem.innerHTML = `<strong>${item.descricao}</strong><br /><small class="text-muted">${Number(item.quantidade).toString()} ${item.unidade}${item.especificacao ? ` — ${item.especificacao}` : ''}</small>`;
      tr.appendChild(celulaItem);
      const campos = [
        { nome: 'preco', tipo: 'number', passo: '0.0001', min: '0', placeholder: '0,0000' },
        { nome: 'desconto', tipo: 'number', passo: '0.01', min: '0', max: '100', placeholder: '0' },
        { nome: 'imposto', tipo: 'number', passo: '0.01', min: '0', max: '100', placeholder: '0' },
        { nome: 'frete', tipo: 'number', passo: '0.01', min: '0', placeholder: '0,00' },
        { nome: 'prazo', tipo: 'number', passo: '1', min: '0', max: '365', placeholder: '0' },
        { nome: 'marca', tipo: 'text', maxComprimento: '100', placeholder: '-' },
        { nome: 'fabricante', tipo: 'text', maxComprimento: '100', placeholder: '-' },
      ];
      campos.forEach((campo) => {
        const td = document.createElement('td');
        const input = document.createElement('input');
        input.type = campo.tipo;
        input.className = 'form-control form-control-sm';
        input.setAttribute(`data-r-${campo.nome}`, '');
        if (campo.passo) input.step = campo.passo;
        if (campo.min !== undefined) input.min = campo.min;
        if (campo.max !== undefined) input.max = campo.max;
        if (campo.maxComprimento) input.maxLength = campo.maxComprimento;
        input.placeholder = campo.placeholder;
        input.setAttribute('aria-label', `${campo.nome} para o item ${item.descricao}`);
        td.appendChild(input);
        tr.appendChild(td);
      });
      const tdRecusa = document.createElement('td');
      const recusa = document.createElement('input');
      recusa.type = 'checkbox';
      recusa.className = 'form-check-input';
      recusa.setAttribute('data-r-recusado', '');
      recusa.setAttribute('aria-label', `Marcar proposta do item ${item.descricao} como recusada`);
      tdRecusa.appendChild(recusa);
      tr.appendChild(tdRecusa);
      recusa.addEventListener('change', () => {
        tr.querySelectorAll('input:not([data-r-recusado])').forEach((input) => { input.disabled = recusa.checked; });
      });
      tbody.appendChild(tr);
    });
    tabela.appendChild(tbody);
    wrapper.appendChild(tabela);
    secao.appendChild(wrapper);

    const acoes = document.createElement('div');
    acoes.className = 'form-actions mt-2';
    const enviador = document.createElement('button');
    enviador.className = 'btn btn-primary';
    enviador.type = 'button';
    enviador.textContent = 'Registrar proposta';
    acoes.appendChild(enviador);
    secao.appendChild(acoes);
    alvo.appendChild(secao);
    enviador.focus();

    enviador.addEventListener('click', async () => {
      lock(enviador, true);
      try {
        const erros = [];
        const itensPayload = [...tbody.querySelectorAll('tr[data-resposta-item]')].map((linha, indice) => {
          const item = itens[indice];
          const campo = (nome) => linha.querySelector(`[data-r-${nome}]`);
          const recusado = campo('recusado').checked;
          const decimal = (nome, obrigatoria = false) => {
            const valor = campo(nome).value.trim();
            if (valor === '') return 0;
            const numero = Number(valor);
            if (!Number.isFinite(numero) || numero < 0) { erros.push(`Valor inválido em “${nome}” para o item ${item.ordem ?? indice + 1}.`); return 0; }
            if (obrigatoria && numero === 0) erros.push(`Preço unitário é obrigatório quando a proposta não é recusada (item ${item.ordem ?? indice + 1}).`);
            return numero;
          };
          const percentual = (nome) => {
            const valor = campo(nome).value.trim();
            if (valor === '') return 0;
            const numero = Number(valor);
            if (!Number.isFinite(numero) || numero < 0 || numero > 100) erros.push(`${nome === 'desconto' ? 'Desconto' : 'Imposto'} deve estar entre 0 e 100 (item ${item.ordem ?? indice + 1}).`);
            return numero;
          };
          const inteiro = () => {
            const valor = campo('prazo').value.trim();
            if (valor === '') return 0;
            const numero = Number(valor);
            if (!Number.isInteger(numero) || numero < 0 || numero > 365) erros.push(`Prazo deve ser um número inteiro entre 0 e 365 dias (item ${item.ordem ?? indice + 1}).`);
            return numero;
          };
          const preco = recusado ? 0 : decimal('preco', true);
          return {
            requisicaoItemId: item.requisicaoItemId,
            precoUnitario: recusado ? 0 : preco,
            desconto: recusado ? 0 : percentual('desconto'),
            imposto: recusado ? 0 : percentual('imposto'),
            frete: recusado ? 0 : decimal('frete'),
            prazoDias: recusado ? 0 : inteiro(),
            marca: recusado ? null : (campo('marca').value.trim() || null),
            fabricante: recusado ? null : (campo('fabricante').value.trim() || null),
            recusado,
          };
        });
        if (erros.length > 0) { feedback(raiz, erros.join(' '), true); return; }
        await request(`${BASE}/cotacoes/${cotacaoId}/respostas`, {
          method: 'POST',
          headers: { 'Idempotency-Key': chaveSessao(`cotacao:responder:${cotacaoId}:${conviteId}`) },
          body: JSON.stringify({ conviteId, conviteVersion, itens: itensPayload }),
        });
        feedback(raiz, `Proposta de ${fornecedorNome} registrada com sucesso.`);
        setTimeout(() => window.location.reload(), 1200);
      } catch (erro) {
        feedback(raiz, erro.message, true);
      } finally {
        lock(enviador, false);
      }
    });
  }

  raiz.querySelector('[data-encerrar-rodada]')?.addEventListener('click', async (evento) => {
    const botao = evento.currentTarget;
    const motivo = raiz.querySelector('[data-motivo-encerramento]').value.trim();
    if (motivo.length < 5 || motivo.length > 500) { feedback(raiz, 'O motivo do encerramento precisa ter entre 5 e 500 caracteres.', true); return; }
    if (!window.confirm('Encerrar esta rodada? O saldo reservado será liberado para uma nova rodada de cotação.')) return;
    lock(botao, true);
    try {
      await request(`${BASE}/cotacoes/${cotacaoId}/encerrar`, {
        method: 'POST',
        headers: { 'Idempotency-Key': chaveSessao(`cotacao:encerrar:${cotacaoId}`) },
        body: JSON.stringify({ motivo, version: versao }),
      });
      window.location.reload();
    } catch (erro) {
      feedback(raiz, erro.message, true);
    } finally {
      lock(botao, false);
    }
  });
}

/* ------------------------------------------------------------------ */
/* Comparativo                                                         */
/* ------------------------------------------------------------------ */
const comparativo = document.querySelector('[data-cotacao-comparativo]');
if (comparativo) iniciarComparativo(comparativo);

function iniciarComparativo(raiz) {
  const cotacaoId = raiz.dataset.id;
  const versao = Number(raiz.dataset.version);
  const arredondar = (valor) => Math.round(valor * 100) / 100;
  const menorPorLinha = new Map();
  raiz.querySelectorAll('[data-selecao-radio]').forEach((radio) => {
    const linha = radio.dataset.linha;
    const custo = Number(radio.dataset.custo);
    menorPorLinha.set(linha, Math.min(menorPorLinha.get(linha) ?? Infinity, custo));
  });

  function avaliarLinha(linha) {
    const marcado = raiz.querySelector(`input[data-selecao-radio][data-linha="${linha}"]:checked`);
    const estado = raiz.querySelector(`[data-justificativa-estado][data-linha="${linha}"]`);
    const campo = raiz.querySelector(`[data-justificativa][data-linha="${linha}"]`);
    if (!estado || !campo) return;
    if (!marcado) { estado.textContent = ''; campo.required = false; return; }
    const acimaDoMenor = arredondar(Number(marcado.dataset.custo)) > arredondar(menorPorLinha.get(linha));
    campo.required = acimaDoMenor;
    estado.textContent = acimaDoMenor
      ? 'Custo selecionado acima do menor custo registrado: justificativa obrigatória (mínimo 10 caracteres).'
      : '';
  }

  raiz.querySelectorAll('[data-selecao-radio]').forEach((radio) => {
    radio.addEventListener('change', () => avaliarLinha(radio.dataset.linha));
  });

  raiz.querySelector('[data-concluir-selecao]')?.addEventListener('click', async (evento) => {
    const botao = evento.currentTarget;
    const selecoes = new Map();
    raiz.querySelectorAll('input[data-selecao-radio]:checked').forEach((radio) => selecoes.set(radio.dataset.linha, radio));
    const erros = [];
    if (selecoes.size === 0) erros.push('Escolha a proposta vencedora de ao menos um item.');
    const itens = [];
    for (const [linha, radio] of selecoes) {
      const acimaDoMenor = arredondar(Number(radio.dataset.custo)) > arredondar(menorPorLinha.get(linha));
      const campoJustificativa = raiz.querySelector(`[data-justificativa][data-linha="${linha}"]`);
      const justificativa = (campoJustificativa?.value ?? '').trim();
      if (acimaDoMenor && justificativa.length < 10) erros.push(`A justificativa do item ${linha} precisa ter pelo menos 10 caracteres.`);
      itens.push({ requisicaoItemId: linha, conviteId: radio.dataset.convite, justificativa: justificativa || null });
    }
    if (erros.length > 0) { feedback(raiz, erros.join(' '), true); return; }
    if (!window.confirm('Registrar a seleção? Esta ação cria um pedido por fornecedor vencedor e não pode ser desfeita nesta tela.')) return;
    lock(botao, true);
    try {
      const resposta = await request(`${BASE}/cotacoes/${cotacaoId}/selecionar`, {
        method: 'POST',
        headers: { 'Idempotency-Key': chaveSessao(`cotacao:selecionar:${cotacaoId}`) },
        body: JSON.stringify({ version: versao, itens }),
      });
      const pedidosGerados = resposta?.pedidos ?? [];
      feedback(raiz, `Seleção registrada. ${pedidosGerados.length} pedido(s) gerado(s).`);
      setTimeout(() => { window.location.href = `${WEB}/Cotacoes/${cotacaoId}`; }, 1500);
    } catch (erro) {
      feedback(raiz, erro.message, true);
    } finally {
      lock(botao, false);
    }
  });
}
