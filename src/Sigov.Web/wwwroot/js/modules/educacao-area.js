/* Ciclo 2 Sprint 6 (BR-6.1 a BR-6.6) — motor das telas reais da Educação Avançada.
   - KPIs com origem: nome da métrica + endpoint + timestamp da consulta (zero ≠ indisponível).
   - Toda ação chama a API real; falhas nomeadas aparecem exatamente como retornadas (regra 13).
   - Ações destrutivas: confirmação via _ConfirmModal com justificativa obrigatória quando exigida. */
(function () {
  'use strict';

  function qs(sel, root) { return (root || document).querySelector(sel); }
  function esc(v) {
    return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }
  function fmtDate(iso) {
    if (!iso) return '—';
    try { var d = new Date(iso); return d.toLocaleDateString('pt-BR') + ' ' + d.toLocaleTimeString('pt-BR'); }
    catch (e) { return String(iso); }
  }
  function nowUtc() {
    var d = new Date();
    return d.toISOString().replace('T', ' ').slice(0, 19) + ' UTC';
  }
  function todayIso() { return new Date().toISOString().slice(0, 10); }

  function apiBase() {
    var cfg = window.EduAreaConfig || {};
    if (window.Sigov_API_BASE_URL) return String(window.Sigov_API_BASE_URL).replace(/\/+$/, '');
    if (cfg.apiBaseOverride) return String(cfg.apiBaseOverride).replace(/\/+$/, '');
    return window.location.protocol === 'https:' ? window.location.origin : 'http://localhost:5001';
  }

  async function api(path, opts) {
    opts = opts || {};
    // caminhos da area sao relativos ao prefixo tipado (ex.: api/educacao/merenda);
    // o origem absoluto vem de apiBase() e o prefixo vem da configuracao C# da view.
    var cfgForPath = window.EduAreaConfig || {};
    if (cfgForPath.apiBase && typeof path === 'string' && path.charAt(0) === '/') {
      path = '/' + String(cfgForPath.apiBase).replace(/^\/+|\/+$/g, '') + path;
    }
    var headers = { 'Content-Type': 'application/json' };
    var tok = null;
    try { tok = window.sigovApi && window.sigovApi.getAuthToken ? await window.sigovApi.getAuthToken() : null; } catch (e) { tok = null; }
    if (tok) headers['Authorization'] = 'Bearer ' + tok;
    var res = await fetch(apiBase() + path, {
      method: opts.method || 'GET',
      headers: headers,
      credentials: 'omit',
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined
    });
    if (opts.raw === 'buffer') return await res.arrayBuffer();
    var text = await res.text();
    var j = null;
    try { j = text ? JSON.parse(text) : null; } catch (e) { j = null; }
    if (!res.ok || (j && j.success === false)) {
      var msg = (j && j.message) || (j && j.detail) || ('HTTP ' + res.status);
      var err = new Error(msg);
      err.status = res.status;
      throw err;
    }
    return j && j.data !== undefined ? j.data : j;
  }

  function toast(kind, msg) {
    if (window.SigovToast) window.SigovToast[kind](msg);
    else console[kind === 'success' ? 'log' : 'warn']('[edu-area] ' + msg);
  }
  function inlineError(container, msg) {
    if (!container) return;
    if (window.SigovAlert && window.SigovAlert.inline) { container.innerHTML = ''; window.SigovAlert.inline(container, msg, 'danger'); }
    else { container.innerHTML = '<div class="alert alert-danger" role="alert">' + esc(msg) + '</div>'; }
  }
  function clearError(container) { if (container) container.innerHTML = ''; }

  function setLoading(on) {
    var el = qs('#eduListWrap .sigov-loading');
    if (el) el.hidden = !on;
  }

  async function confirmar(opts, motivo) {
    var ok = false, valor = '';
    try {
      ok = await window.SigovConfirm.show({ title: opts.title, message: opts.message, variant: opts.variant || 'primary', confirmText: opts.confirmText || 'Confirmar' });
    } catch (e) { ok = false; }
    return { ok: ok, valor: valor };
  }

  /* Confirmação com campo de justificativa: injeta o input no corpo do modal aberto e lê o
     valor antes de fechar. Motivo obrigatório bloqueia a chamada quando está vazio. */
  async function confirmarComMotivo(opts, motivoObrigatorio) {
    var modalEl = document.getElementById('sigovConfirmModal');
    var msgEl = document.getElementById('sigovConfirmMessage');
    var input = null;
    var p = confirmar(opts, motivoObrigatorio);
    if (motivoObrigatorio !== false && msgEl) {
      input = document.createElement('input');
      input.className = 'form-control edu-confirm-motivo';
      input.type = 'text';
      input.placeholder = motivoObrigatorio === true ? 'Justificativa (obrigatória)' : 'Justificativa (opcional)';
      input.setAttribute('aria-label', 'Justificativa da operação');
      msgEl.appendChild(input);
    }
    var r = await p;
    if (input) {
      if (!r.ok) input.remove();
      else {
        r.valor = input.value.trim();
        input.remove();
        if (motivoObrigatorio === true && !r.valor) {
          toast('warning', 'Justificativa obrigatória para esta operação.');
          return { ok: false, valor: '' };
        }
      }
    }
    return r;
  }

  function buildBody(tpl, extras) {
    var out = {};
    Object.keys(tpl || {}).forEach(function (k) {
      var v = tpl[k];
      if (v === '__motivo__') v = (extras.motivo != null ? extras.motivo : '');
      else if (v === '__observacao__') v = (extras.motivo != null ? extras.motivo : '');
      else if (v === '__hoje__') v = todayIso();
      out[k] = v;
    });
    return out;
  }

  function statusPill(status) {
    var s = String(status || '').toUpperCase();
    var cls = 'edu-status-pill';
    if (/ALERTA|PEND|ABERT|EM_ANDAMENTO|INCONSIST/.test(s)) cls += ' edu-status-pill--alerta';
    else if (/ATIV|CONCL|ACEIT|CORRIG|FECHAD|HABILITAD|REGULAR/.test(s)) cls += ' edu-status-pill--ok';
    return '<span class="' + cls + '">' + esc(status || '—') + '</span>';
  }

  function actionApplies(act, row) {
    var st = String(row.status || '');
    if (act.matchStatus && act.matchStatus.length && act.matchStatus.indexOf(st) === -1) return false;
    if (act.exceptStatus && act.exceptStatus.length && act.exceptStatus.indexOf(st) !== -1) return false;
    return true;
  }

  function init() {
    var cfg = window.EduAreaConfig;
    var root = qs('.edu-area[data-edu-page]');
    if (!cfg || !root) return;
    var page = root.dataset.eduPage;
    var pg = (cfg.pages || {})[page];
    var errorSlot = qs('#eduError', root);
    if (!pg || !pg.list) {
      inlineError(errorSlot, 'EDU_AREA_PAGINA_SEM_ENDPOINT: a página "' + page + '" não possui endpoint tipado definido.');
      return;
    }

    var state = { rows: [], page: 1, total: 0, busca: '', lastListAt: null, lastKpiAt: null };
    var urlParams = new URLSearchParams(window.location.search);
    var statusFiltro = urlParams.get('status') || '';

    var kpisSlot = qs('#eduKpis', root);
    var tableSlot = qs('#eduTableSlot', root);
    var emptySlot = qs('#eduEmptySlot', root);
    var pageInfo = qs('#eduPageInfo', root);
    var panelSlot = qs('#eduPanel', root);

    function renderKpis(data, endpoint, error) {
      kpisSlot.innerHTML = '';
      var defs = ((cfg.dashboard || {}).kpis) || [];
      if (!defs.length) return;
      defs.forEach(function (k) {
        var card = document.createElement('article');
        card.className = 'edu-kpi';
        var value;
        if (error) value = '!';
        else {
          var raw = data ? data[k.path] : undefined;
          value = (raw === null || raw === undefined) ? '—' : (typeof raw === 'number' ? raw.toLocaleString('pt-BR') : esc(raw));
          if (typeof raw === 'number' && raw === 0) value = '0';
        }
        card.innerHTML =
          '<small>' + esc(k.label) + '</small>' +
          '<strong class="edu-kpi__value">' + value + '</strong>' +
          '<small class="kpi-src">fonte: GET <code>' + esc(endpoint) + '</code> · ' + esc(state.lastKpiAt || '—') + (error ? ' · ' + esc(error) : '') + '</small>';
        kpisSlot.appendChild(card);
      });
    }

    function loadKpis() {
      var ep = (cfg.dashboard || {}).endpoint;
      if (!ep) return Promise.resolve();
      setLoading(true);
      state.lastKpiAt = nowUtc();
      return api(ep).then(function (data) {
        renderKpis(data, cfg.apiBase + ep);
      }, function (err) {
        renderKpis(null, cfg.apiBase + ep, err.message || 'falha');
        inlineError(errorSlot, (err.message || 'Falha ao carregar indicadores') + ' — os KPIs exibem o estado real.');
      });
    }

    function listQuery(extra) {
      var q = [];
      if (statusFiltro) q.push('status=' + encodeURIComponent(statusFiltro));
      if (extra && extra.pagina) q.push('pagina=' + extra.pagina);
      q.push('tamanho=30');
      return q.length ? ('?' + q.join('&')) : '';
    }

    function visibleRows() {
      var b = state.busca.trim().toLowerCase();
      if (!b) return state.rows;
      return state.rows.filter(function (r) {
        return Object.keys(r).some(function (k) {
          return String(r[k]).toLowerCase().indexOf(b) !== -1;
        });
      });
    }

    function renderTable(rows, listMeta) {
      tableSlot.innerHTML = '';
      emptySlot.hidden = true;
      if (!rows.length) {
        var tpl = document.getElementById('eduEmptyTpl');
        if (tpl) {
          emptySlot.appendChild(tpl.content.cloneNode(true));
          emptySlot.hidden = false;
        }
        pageInfo.textContent = 'Página ' + state.page + ' · 0 registros';
        return;
      }
      var cols = cfg.columns || [];
      var acts = pg.actions || [];
      var table = document.createElement('table');
      table.className = 'edu-table';
      table.setAttribute('role', 'grid');
      var thead = '<thead><tr><th>ID</th>' + cols.map(function (c) { return '<th>' + esc(c.label) + '</th>'; }).join('') + (acts.length ? '<th>Ações</th>' : '') + '</tr></thead>';
      var tbody = '<tbody>';
      rows.forEach(function (r) {
        var idx = state.rows.indexOf(r);
        var cells = cols.map(function (c) {
          var v = r[c.key];
          if (c.key === 'status') return '<td>' + statusPill(v) + '</td>';
          if (c.key === 'updatedAt' || c.key === 'createdAt') return '<td>' + esc(fmtDate(v)) + '</td>';
          if (typeof v === 'number') return '<td>' + esc(v.toLocaleString('pt-BR')) + '</td>';
          return '<td>' + esc(v == null || v === '' ? '—' : String(v).slice(0, 80)) + '</td>';
        }).join('');
        var btns = acts.map(function (a, ai) {
          if (!actionApplies(a, r)) return '';
          return '<button type="button" class="sigov-btn sigov-btn--ghost edu-row-act" data-act="' + ai + '" data-id="' + r.id + '" data-detail="0">' + esc(a.label) + '</button> ';
        }).join('');
        tbody += '<tr class="edu-row" data-row="' + idx + '"><td>' + esc(r.id) + '</td>' + cells + (acts.length ? '<td>' + btns + '</td>' : '') + '</tr><tr class="edu-detail" hidden data-detail-row="' + idx + '"><td colspan="' + (cols.length + 2 + (acts.length ? 1 : 0)) + '"><dl>' +
          ['descricao', 'tipo', 'quantidade', 'valor', 'codigo'].map(function (f) { return '<dt>' + esc(f) + '</dt><dd>' + esc(r[f] == null || r[f] === '' ? '—' : r[f]) + '</dd>'; }).join('') +
          '<dt>criadoEm</dt><dd>' + esc(fmtDate(r.createdAt)) + '</dd><dt>atualizadoEm</dt><dd>' + esc(fmtDate(r.updatedAt)) + '</dd>' +
          (listMeta ? '<dt>origem</dt><dd>GET ' + esc(listMeta) + '</dd>' : '') +
          '</dl></tr>';
      });
      tbody += '</tbody>';
      table.innerHTML = thead + tbody;
      tableSlot.appendChild(table);
      pageInfo.textContent = 'Página ' + state.page + ' de ' + Math.max(1, Math.ceil(state.total / 30)) + ' · ' + state.total + ' registros no total' + (state.busca ? ' · ' + rows.length + ' no recorte local' : '');
      qs('#eduNext', root).disabled = (state.page * 30) >= state.total;
      qs('#eduPrev', root).disabled = state.page <= 1;
    }

    function loadList() {
      setLoading(true);
      clearError(errorSlot);
      state.lastListAt = nowUtc();
      var meta = cfg.apiBase + pg.list.endpoint;
      return api(pg.list.endpoint + listQuery({ pagina: state.page })).then(function (data) {
        var items = pg.list.path ? (data ? data[pg.list.path] : []) : ((data && data.items) || []);
        state.rows = items || [];
        state.total = (data && typeof data.totalItems === 'number') ? data.totalItems : state.rows.length;
        renderTable(visibleRows(), meta);
      }, function (err) {
        inlineError(errorSlot, err.message || 'Falha ao carregar registros.');
        toast('error', err.message || 'Falha ao carregar registros.');
        renderTable([], null);
      }).then(function () { setLoading(false); }, function () { setLoading(false); });
    }

    function loadPanel() {
      panelSlot.hidden = true;
      if (!pg.panel || !pg.panel.endpoint) return;
      var ep = pg.panel.endpoint;
      api(ep).then(function (data) {
        var fields = pg.panel.fields || [];
        var html = '<strong>' + esc(pg.panel.title || 'Resumo') + '</strong><dl class="edu-conformidade__fields">' +
          fields.map(function (f) {
            var v = data ? data[f.path] : undefined;
            return '<div><dt>' + esc(f.label) + '</dt><dd>' + esc(v == null ? (f.zeroNote || '—') : (typeof v === 'number' ? v.toLocaleString('pt-BR') : v)) + (v === 0 && f.zeroNote ? ' <em>(' + esc(f.zeroNote) + ')</em>' : '') + '</dd></div>';
          }).join('') + '</dl>' +
          '<small class="kpi-src">fonte: GET <code>' + esc(cfg.apiBase + ep) + '</code> · ' + esc(nowUtc()) + '</small>';
        panelSlot.innerHTML = html;
        panelSlot.hidden = false;
      }, function (err) {
        inlineError(errorSlot, err.message || 'Falha ao carregar o painel de apoio.');
      });
    }

    function runAction(ai, id) {
      var acts = pg.actions || [];
      var act = acts[ai];
      if (!act) return;
      var row = state.rows.find(function (r) { return String(r.id) === String(id); }) || { id: id };
      var needMotivo = act.motivo === true;
      var optionalMotivo = act.motivo === 'optional';
      var confirmDef = act.confirm || { title: act.label, message: 'Esta operação será executada na API e ficará auditada.' };
      confirmarComMotivo(confirmDef, needMotivo || optionalMotivo).then(function (res) {
        if (!res.ok) return;
        var body = buildBody(act.body, { motivo: res.valor });
        var path = String(act.path).replace('{id}', String(id));
        var method = act.kind === 'get' ? 'GET' : 'POST';
        setLoading(true);
        clearError(errorSlot);
        api(path, { method: method, body: method === 'POST' ? body : undefined }).then(function (data) {
          var eff = (data && typeof data === 'object' && data.id != null) ? data.id : (data != null ? data : id);
          if (act.kind === 'get') {
            var idx = state.rows.findIndex(function (r) { return String(r.id) === String(id); });
            var det = qs('tr.edu-detail[data-detail-row="' + (idx >= 0 ? idx : 0) + '"]');
            if (det) {
              var cell = det.querySelector('td');
              if (cell) {
                var pre = document.createElement('div');
                pre.innerHTML = '<dl><dt>relatorio</dt><dd><pre style="max-height:280px;overflow:auto;margin:.25rem 0 0">' + esc(JSON.stringify(data, null, 2)).slice(0, 4000) + '</pre></dd><dt>origem</dt><dd>GET ' + esc(cfg.apiBase + path) + ' · ' + esc(nowUtc()) + '</dd></dl>';
                cell.appendChild(pre);
              }
              det.hidden = false;
            }
            toast('success', (act.success || 'Dados carregados') .toString().replace('{id}', String(id)) + ' — dados reais exibidos no detalhe da linha.');
            return;
          }
          toast('success', (act.success || 'Operação concluída') .toString().replace('{id}', String(eff)) + ' — efeito confirmado pela API em ' + nowUtc() + '.');
          loadKpis().then(loadList);
        }, function (err) {
          inlineError(errorSlot, err.message || 'Falha na operação.');
          toast('error', err.message || 'Falha na operação.');
        });
      });
    }

    function pageActionsClick(ev) {
      var btn = ev.target.closest('[data-pageaction]');
      if (!btn) return;
      var acts = pg.pageActions || [];
      var pa = acts[Number(btn.dataset.pageaction)];
      if (!pa) return;
      confirmarComMotivo(pa.confirm || { title: pa.label, message: 'Esta operação será executada na API e ficará auditada.'}, pa.motivo === true || pa.motivo === 'optional').then(function (res) {
        if (!res.ok) return;
        var body = buildBody(pa.body || {}, { motivo: res.valor });
        setLoading(true);
        clearError(errorSlot);
        api(pa.path, { method: 'POST', body: body }).then(function (data) {
          var eff = (data && data.id != null) ? data.id : data;
          toast('success', (pa.success || 'Operação concluída').toString().replace('{id}', String(eff)) + ' — efeito confirmado pela API em ' + nowUtc() + '.');
          loadKpis().then(loadList).then(loadPanel);
        }, function (err) {
          inlineError(errorSlot, err.message || 'Falha na operação.');
          toast('error', err.message || 'Falha na operação.');
          setLoading(false);
        });
      });
    }

    root.addEventListener('click', function (ev) {
      var actBtn = ev.target.closest('.edu-row-act');
      if (actBtn) { runAction(Number(actBtn.dataset.act), actBtn.dataset.id); return; }
      pageActionsClick(ev);
    });

    qs('#eduRefresh', root).addEventListener('click', function () {
      setLoading(true);
      clearError(errorSlot);
      loadKpis().then(loadList).then(loadPanel).then(function () {
        setLoading(false);
        toast('success', 'Painel atualizado em ' + nowUtc() + ': ' + state.total + ' registros no recorte.');
      });
    });

    qs('#eduPrev', root).addEventListener('click', function () { if (state.page > 1) { state.page--; loadList(); } });
    qs('#eduNext', root).addEventListener('click', function () { if (state.page * 30 < state.total) { state.page++; loadList(); } });

    var buscaEl = qs('#eduBusca', root);
    if (buscaEl) buscaEl.addEventListener('input', function () { state.busca = buscaEl.value; renderTable(visibleRows(), null); });

    if (pg.csv) {
      var csvBtn = qs('#eduCsvBtn', root);
      csvBtn.hidden = false;
      csvBtn.addEventListener('click', function () {
        clearError(errorSlot);
        api(pg.csv, { raw: 'buffer' }).then(function (buf) {
          var blob = new Blob([buf], { type: 'text/csv' });
          var a = document.createElement('a');
          a.href = URL.createObjectURL(blob);
          a.download = 'educacao-' + (cfg.area || 'area') + '-' + page + '.csv';
          document.body.appendChild(a);
          a.click();
          a.remove();
          toast('success', 'CSV exportado via ' + cfg.apiBase + pg.csv + ' em ' + nowUtc() + '.');
        }, function (err) {
          inlineError(errorSlot, err.message || 'Falha ao exportar CSV.');
          toast('error', err.message || 'Falha ao exportar CSV.');
        });
      });
    }

    if (pg.pageActions && pg.pageActions.length) {
      var actionsWrap = qs('.edu-area__actions', root);
      pg.pageActions.forEach(function (pa, i) {
        var b = document.createElement('button');
        b.type = 'button';
        b.className = 'sigov-btn ' + (pa.danger ? 'sigov-btn--danger' : 'sigov-btn--ghost');
        b.textContent = pa.label;
        b.setAttribute('data-pageaction', String(i));
        actionsWrap.insertBefore(b, qs('#eduRefresh', root));
      });
    }

    loadKpis();
    loadList();
    loadPanel();
  }

  window.EduArea = { init: init };
})();
