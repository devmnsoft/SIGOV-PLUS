// SIGOV PLUS · Financeiro SIAFIC — Conferência financeira: comparativo dos acumuladores
// registrados (na dotação/empenho/previsão de receita) com os totais calculados pelos documentos.
// Indicadores globais são calculados no servidor sobre todo o escopo do filtro ANTES da paginação.
// Ajuste com justificativa obrigatória: o valor exato é sempre recalculado no servidor; se o
// documento mudar entre a prévia e a confirmação o servidor responde 409 e pede nova prévia.
(function () {
  const F = window.sigovFin;
  if (!F || !document.querySelector('[data-fin-tela="conferencia"]')) return;
  const SIZE = 50;
  let pagina = 1;

  function pagerHtml(p, totalPag, totalItens) {
    return `<div class="d-flex align-items-center gap-2 mt-2 flex-wrap">
      <button type="button" class="btn btn-sm btn-outline-secondary pager-prev" ${p <= 1 ? 'disabled' : ''}>← Anterior</button>
      <span class="small text-muted">Página ${p} de ${totalPag} · ${totalItens} registros</span>
      <button type="button" class="btn btn-sm btn-outline-secondary pager-next" ${p >= totalPag ? 'disabled' : ''}>Próxima →</button>
    </div>`;
  }

  async function carregar() {
    const tb = $('#tbody-conferencia');
    tb.html('<tr><td colspan="7" class="text-muted py-3">Executando conferência…</td></tr>');
    try {
      const r = await F.getJson('/api/financeiro/conferencia' + F.qs({
        page: pagina, pageSize: SIZE,
        tipoDocumento: $('#cf-tipo').val() || null,
        situacao: $('#cf-situacao').val() || null
      }));
      const itens = r.itens || [];
      // Indicadores globais do escopo filtrado (autoridade do servidor, sem perder filtros):
      // totalRegistros / totalDivergencias / somaAbsDiferencas / instante.
      const somaAbs = Number(r.somaAbsDiferencas || 0);
      const qtdDiv = Number(r.totalDivergencias || 0);
      const totalReg = Number(r.totalRegistros || 0);
      document.getElementById('cf-total-diferenca').innerHTML =
        `<span class="${somaAbs !== 0 ? 'text-danger' : 'text-success'}">${F.money(somaAbs)}</span>`;
      document.getElementById('cf-qtd-divergentes').innerHTML =
        `<span class="${qtdDiv !== 0 ? 'text-danger fw-semibold' : 'text-success'}">${qtdDiv}</span>`;
      document.getElementById('cf-atualizado').textContent = r.instante ? new Date(r.instante).toLocaleString('pt-BR') : '—';
      tb.html(itens.length ? itens.map(i => {
        const dif = Number(i.diferenca);
        return `<tr class="${dif !== 0 ? 'table-warning' : ''}">
          <td><span class="badge bg-light text-dark border">${F.escapa(i.tipoDocumento)}</span></td>
          <td>#${i.documentoId}${i.documentoNumero ? ' · ' + F.escapa(i.documentoNumero) : ''}</td>
          <td class="text-end">${F.money(i.valorRegistrado)}</td>
          <td class="text-end">${F.money(i.valorCalculado)}</td>
          <td class="text-end fw-semibold ${dif !== 0 ? 'text-danger' : 'text-success'}">${dif !== 0 ? F.money(dif) : '—'}</td>
          <td class="small">${i.origemDivergencia ? F.escapa(i.origemDivergencia) : '<span class="text-muted">Conferido sem divergência</span>'}</td>
          <td class="text-nowrap">${dif !== 0
            ? `<button type="button" class="btn btn-sm btn-warning btn-ajustar" data-tipo="${F.escapa(i.tipoDocumento)}" data-id="${i.documentoId}" data-reg="${i.valorRegistrado}" data-cal="${i.valorCalculado}">Ajustar</button>`
            : '<span class="badge text-bg-success">OK</span>'}</td>
        </tr>`;
      }).join('') : '<tr><td colspan="7" class="text-muted py-3">Nenhum documento para comparar neste exercício.</td></tr>');
      // Pager real com totais gerais (a resposta traz totalRegistros independentemente da página).
      const totalPag = Math.max(1, Math.ceil(totalReg / SIZE));
      $('#pager-conferencia').html(pagerHtml(r.page || pagina, totalPag, totalReg));
    } catch (err) { F.falha('Falha ao executar a conferência', err); }
  }

  function ajustar(btn) {
    const tipo = btn.getAttribute('data-tipo');
    const id = Number(btn.getAttribute('data-id'));
    const reg = Number(btn.getAttribute('data-reg'));
    const cal = Number(btn.getAttribute('data-cal'));
    F.confirmarReversao({
      titulo: `Ajuste do acumulador ${F.escapa(tipo)} · documento #${id}`,
      linhas: [
        { rotulo: 'Registrado no acumulador (prévia)', valor: F.money(reg) },
        { rotulo: 'Calculado pelos documentos (prévia)', valor: F.money(cal) },
        { rotulo: 'Valor do acumulador após o ajuste', valor: F.money(cal) }
      ],
      campos: [{ id: 'justificativa', tipo: 'textarea', label: 'Justificativa (obrigatória — registrada na auditoria)', obrigatorio: true }],
      pergunta: 'O valor exato é sempre recalculado no servidor no momento da confirmação e aplicado em transação única, auditado com usuário, data e correlation ID. Se o documento mudar entre esta prévia e a confirmação, o sistema pedirá para refazer a prévia.',
      onConfirm: async v => {
        try {
          const body = { documentoId: id, tipoDocumento: tipo, justificativa: v.justificativa };
          const chave = F.chaveAcao('fin.conferencia.ajustar:' + id, body);
          await F.postJson('/api/financeiro/conferencia/ajustar', body, chave);
          F.concluirAcao('fin.conferencia.ajustar:' + id, body);
          F.toast('Ajuste aplicado e auditado.', 'success');
          carregar();
          return true;
        } catch (err) {
          // Falha recuperável: a chave idempotente e a justificativa ficam preservadas na tela.
          F.falha('Não foi possível aplicar o ajuste', err);
          return false;
        }
      }
    });
  }

  (function init() {
    $('#btn-cf-filtrar').on('click', () => { pagina = 1; carregar(); });
    $('#btn-cf-limpar').on('click', () => { $('#cf-tipo').val(''); $('#cf-situacao').val(''); pagina = 1; carregar(); });
    $('#pager-conferencia').on('click', '.pager-prev', () => { if (pagina > 1) { pagina--; carregar(); } });
    $('#pager-conferencia').on('click', '.pager-next', () => { pagina++; carregar(); });
    $('#tbody-conferencia').on('click', '.btn-ajustar', function () { ajustar(this); });
    carregar();
  })();
})();
