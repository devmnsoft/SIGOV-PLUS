// SIGOV PLUS · Financeiro SIAFIC — Painel do exercício: KPIs reais via API.
(function () {
  const F = window.sigovFin;
  if (!F) return;

  function pct(v, t) {
    const n = Number(v), d = Number(t);
    return d > 0 ? Math.min(100, (n / d) * 100) : 0;
  }

  async function load() {
    const statusEl = document.getElementById('financeiro-dashboard-status');
    try {
      const d = await F.getJson('/api/financeiro/dashboard');
      const dp = d.despesa || {}, rc = d.receita || {};

      const mapa = {
        orcamento: dp.orcamentoAutorizado,
        empenhado: dp.empenhado,
        liquidado: dp.liquidado,
        pago: dp.pago,
        saldo: dp.saldoDisponivel,
        prevista: rc.receitaPrevista,
        lancada: rc.receitaLancada,
        arrecadada: rc.receitaArrecadada,
        arrecadar: rc.receitaPrevista != null && rc.receitaArrecadada != null ? rc.receitaPrevista - rc.receitaArrecadada : null
      };
      document.querySelectorAll('[data-kpi]').forEach(el => {
        const v = mapa[el.getAttribute('data-kpi')];
        el.textContent = F.money(v == null ? 0 : v);
        el.classList.remove('placeholder-glow');
      });

      ['empenhado', 'liquidado', 'pago'].forEach(k => {
        const bar = document.querySelector(`[data-bar="${k}"] i`);
        if (bar) bar.style.width = pct(dp[k], dp.orcamentoAutorizado).toFixed(1) + '%';
      });

      const avisos = [];
      if (window.Sigov_API_ERRO_EXERCICIO) avisos.push(['danger', window.Sigov_API_ERRO_EXERCICIO]);
      if (dp.saldoDisponivel != null && dp.saldoDisponivel < 0) avisos.push(['danger', 'Saldo disponível do orçamento está negativo.']);
      if (dp.orcamentoAutorizado != null && dp.empenhado != null && dp.empenhado > dp.orcamentoAutorizado) avisos.push(['warning', 'Empenhado excede o orçamento autorizado (dotação vigente).']);
      if (dp.liquidado != null && dp.empenhado != null && dp.liquidado > dp.empenhado) avisos.push(['danger', 'Liquidado supera o valor empenhado — verifique na conferência financeira.']);
      if (dp.pago != null && dp.liquidado != null && dp.pago > dp.liquidado) avisos.push(['danger', 'Pago supera o valor liquidado — verifique na conferência financeira.']);

      const alvo = document.getElementById('financeiro-alertas');
      if (alvo) alvo.innerHTML = avisos.length
        ? avisos.map(([t, m]) => `<div class="alert alert-${t} py-2 small mb-2">${F.escapa(m)}</div>`).join('') + '<a href="/Financeiro/Conferencia">Abrir conferência financeira →</a>'
        : '<div class="alert alert-success py-2 small mb-2">Nenhum ponto de atenção no momento.</div><a href="/Financeiro/Conferencia">Abrir conferência financeira →</a>';

      if (statusEl) statusEl.textContent = 'Dados reais carregados em ' + new Date(d.atualizadoEm).toLocaleString('pt-BR');
    } catch (err) {
      if (statusEl) statusEl.textContent = 'Falha ao carregar os dados reais do exercício.';
      F.falha('Não foi possível carregar o painel do exercício', err);
    }
  }

  load();
})();
