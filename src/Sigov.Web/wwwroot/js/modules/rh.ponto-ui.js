// SIGOV PLUS — UI funcional do ciclo de ponto (RC-EVO-RH §10).
// Jornadas/escalas/batidas/ajustes/justificativas/apuração/homologação/reabertura/
// integração à folha/espelho/portal/pendências.
//
// Regras desta camada:
//  - Todo conteúdo dinâmico entra no DOM apenas por createElement/textContent
//    (sem interpolação em innerHTML) — XSS-safe por construção.
//  - O JSONB legado grava datas/horas/campos em casing misto (CRUD genérico usa
//    PascalCase; apuração/espelho usam camelCase). Toda leitura passa por pick().
//  - Autoridade é a API (Bearer via sigovApi + cookie Web); nenhuma regra de
//    negócio é reimplementada aqui — a UI só apresenta o que o backend devolveu.
(function () {
  'use strict';
  if (window.SigovRhPonto) return;

  /* ============================ primitivas DOM ============================ */

  function el(tag, attrs, ...kids) {
    const node = document.createElement(tag);
    for (const [k, v] of Object.entries(attrs || {})) {
      if (v === null || v === undefined || v === false) continue;
      if (k === 'text') { node.textContent = v; continue; }
      if (k === 'value') { node.value = v === true ? '' : String(v); continue; }
      if (k.indexOf('on') === 0 && typeof v === 'function') { node.addEventListener(k.slice(2).toLowerCase(), v); continue; }
      if (v === true) node.setAttribute(k, '');
      else node.setAttribute(k, String(v));
    }
    const flat = [];
    (function walk(list) {
      for (const item of list) {
        if (item === null || item === undefined || item === false) continue;
        if (Array.isArray(item)) walk(item);
        else flat.push(item);
      }
    })(kids);
    for (const c of flat) {
      node.appendChild(typeof c === 'string' || typeof c === 'number' ? document.createTextNode(String(c)) : c);
    }
    return node;
  }

  // Lookup tolerante a casing: o JSONB RH mistura PascalCase (CRUD genérico) e
  // camelCase (apuração/espelho escritos explicitamente).
  function pick(obj, ...keys) {
    if (!obj || typeof obj !== 'object') return undefined;
    for (const k of keys) { const v = obj[k]; if (v !== undefined && v !== null) return v; }
    const lower = {};
    for (const k of Object.keys(obj)) lower[String(k).toLowerCase()] = obj[k];
    for (const k of keys) { const v = lower[String(k).toLowerCase()]; if (v !== undefined && v !== null) return v; }
    return undefined;
  }

  /* ============================ formatação ============================ */

  function fmtMin(m) {
    if (m === null || m === undefined || m === '' || isNaN(Number(m))) return '—';
    const n = Math.round(Number(m));
    const sign = n < 0 ? '-' : '';
    const a = Math.abs(n);
    const h = Math.floor(a / 60);
    const mm = a % 60;
    return sign + (h ? h + 'h' : '') + (h && mm ? ' ' : '') + (mm ? mm + 'min' : (h ? '' : '0min'));
  }

  function money(v) {
    if (v === null || v === undefined || v === '' || isNaN(Number(v))) return '—';
    return Number(v).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }

  function num(v) {
    if (v === null || v === undefined || v === '' || isNaN(Number(v))) return '—';
    return Number(v).toLocaleString('pt-BR');
  }

  function fmtDate(v) {
    if (!v) return '—';
    const s = String(v);
    if (/^\d{4}-\d{2}-\d{2}$/.test(s)) return s.slice(8, 10) + '/' + s.slice(5, 7) + '/' + s.slice(0, 4);
    // Data ISO completa: exibe o valor registrado no fuso operacional (componentes literais),
    // sem converter para o fuso local do navegador (a engine grava o fuso da operação no offset).
    const m = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
    if (m) return m[3] + '/' + m[2] + '/' + m[1];
    const d = new Date(s);
    return isNaN(d.getTime()) ? s : d.toLocaleDateString('pt-BR');
  }

  function fmtDateTime(v) {
    if (!v) return '—';
    const s = String(v);
    // Timestamps com offset explícito (fuso operacional da operação): mostra o horário
    // registrado como foi gravado — nunca recalculado no fuso local do visualizador.
    const m = s.match(/^(\d{4})-(\d{2})-(\d{2})[T ](\d{2}):(\d{2})(?::(\d{2}))?/);
    if (m) return m[3] + '/' + m[2] + '/' + m[1] + ', ' + m[4] + ':' + m[5] + ':' + (m[6] || '00');
    const d = new Date(s);
    return isNaN(d.getTime()) ? s : d.toLocaleString('pt-BR');
  }

  // "HH:mm:ss" (TimeOnly) ou ISO -> "HH:mm"
  function fmtTime(t) {
    if (!t) return '—';
    const s = String(t);
    const m = s.match(/(\d{2}):(\d{2})/);
    return m ? m[1] + ':' + m[2] : s;
  }

  const DIAS_SEMANA = ['', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb', 'Dom'];
  function diasLabel(s) {
    if (!s) return '—';
    const parts = String(s).split(/[,;\s]+/).filter(Boolean);
    const out = [];
    for (const p of parts) {
      const i = Number(p);
      if (i >= 1 && i <= 7) out.push(DIAS_SEMANA[i]);
      else out.push(p);
    }
    return out.length ? out.join(', ') : '—';
  }

  const TIPO_BATIDA_LABEL = {
    entrada: 'Entrada',
    saida: 'Saída',
    intervaloinicio: 'Início do intervalo',
    intervalofim: 'Fim do intervalo',
    ajuste: 'Ajuste'
  };
  function tipoBatidaLabel(t) {
    if (!t) return '—';
    return TIPO_BATIDA_LABEL[String(t).trim().toLowerCase()] || String(t);
  }

  function statusTone(s) {
    const u = String(s || '').toUpperCase();
    if (['HOMOLOGADA', 'APROVADA', 'APROVADO', 'ATIVO', 'ENTREGUE', 'EMITIDO', 'PAGO'].includes(u)) return 'ok';
    if (['REPROVADA', 'REPROVADO', 'INATIVA', 'CANCELADA', 'FALHOU', 'ERRO', 'ENCERRADA', 'FECHADA'].includes(u)) return 'danger';
    if (['APURADA', 'PENDENTE', 'REABERTA', 'ANALISE', 'EM_ANALISE', 'RASCUNHO', 'SOLICITADA', 'ABERTA'].includes(u)) return 'info';
    return null;
  }

  function chip(text, tone) {
    const c = el('span', { class: 'status-chip', title: String(text || '') });
    if (tone === 'warning') { c.style.background = '#fff3cd'; c.style.color = '#997404'; }
    else if (tone === 'danger') { c.style.background = '#fde2e2'; c.style.color = '#b42318'; }
    else if (tone === 'info') { c.style.background = '#e1effe'; c.style.color = '#174ea6'; }
    c.textContent = text;
    return c;
  }

  function statusChip(s) { return chip(s ? String(s) : '—', statusTone(s)); }

  function actBtn(label, cls, onclick, opts = {}) {
    return el('button', {
      type: 'button',
      class: 'btn btn-sm ' + cls,
      onclick,
      disabled: opts.disabled ? true : null,
      'aria-label': opts.title || label
    }, opts.title ? label + ' (' + opts.title + ')' : label);
  }

  /* ============================ HTTP (via sigovApi/Bearer) ============================ */

  function normError(e, prefix) {
    prefix = prefix || 'Operação falhou.';
    let msg = e && e.message ? String(e.message) : '';
    if (!msg || msg === 'Failed to fetch' || msg === 'NetworkError when attempting to fetch resource.') {
      msg = prefix + (e && e.status ? ' (HTTP ' + e.status + ')' : ' Verifique a conexão com a API.');
    } else if (msg.trim() === '403') {
      msg = 'Sem permissão para executar esta ação (403). Confira seu perfil em RH.';
    }
    const err = new Error(msg);
    err.status = e && e.status;
    err.correlationId = e && e.correlationId;
    return err;
  }

  async function req(path, opts) {
    try {
      return await window.sigovApi.request(path, opts || {});
    } catch (e) {
      throw normError(e);
    }
  }

  async function fetchList(path, opts) {
    opts = opts || {};
    const q = new URLSearchParams();
    q.set('page', '1');
    q.set('pageSize', String(opts.pageSize || 100));
    if (opts.termo) q.set('termo', opts.termo);
    const sep = path.indexOf('?') >= 0 ? '&' : '?';
    const env = await req(path + sep + q.toString());
    const d = env && env.data;
    const items = Array.isArray(d && d.items) ? d.items : (Array.isArray(d) ? d : []);
    return { items: items, total: Number((d && d.totalItems) || items.length) };
  }

  /* ============================ toast (auto-dismiss) ============================ */

  function toast(msg, kind) {
    kind = kind || 'success';
    let host = document.getElementById('sigov-rp-toasts');
    if (!host) {
      host = el('div', { id: 'sigov-rp-toasts', role: 'status', 'aria-live': 'polite' });
      host.style.cssText = 'position:fixed;top:1rem;right:1rem;z-index:2100;display:flex;flex-direction:column;gap:.5rem;max-width:min(92vw,440px);';
      document.body.appendChild(host);
    }
    const map = { success: 'alert-success', error: 'alert-danger', warning: 'alert-warning', info: 'alert-info' };
    const box = el('div', { class: 'alert ' + (map[kind] || 'alert-info') + ' shadow-sm', role: 'alert' },
      String(msg),
      el('button', { type: 'button', class: 'btn-close ms-auto', 'aria-label': 'Fechar aviso', onclick: function () { box.remove(); } }));
    host.appendChild(box);
    setTimeout(function () {
      if (box.isConnected) { box.style.transition = 'opacity .4s'; box.style.opacity = '0'; setTimeout(function () { box.remove(); }, 420); }
    }, 7000);
  }

  /* ============================ modal (Bootstrap 5) ============================ */

  let modalSeq = 0;
  function ensureModal(parent, title) {
    const id = 'rp-modal-' + (++modalSeq) + '-' + Math.random().toString(36).slice(2, 8);
    const holder = el('div', { class: 'modal fade', id: id, tabindex: '-1', 'aria-hidden': 'true', 'aria-label': title },
      el('div', { class: 'modal-dialog modal-dialog-centered modal-lg' },
        el('div', { class: 'modal-content' },
          el('div', { class: 'modal-header' },
            el('h5', { class: 'modal-title', text: title }),
            el('button', { type: 'button', class: 'btn-close', 'data-bs-dismiss': 'modal', 'aria-label': 'Fechar' })
          ),
          el('div', { class: 'modal-body' }),
          el('div', { class: 'modal-footer' })
        )
      )
    );
    (parent || document.body).appendChild(holder);
    const bs = function () { return (window.bootstrap && window.bootstrap.Modal) ? window.bootstrap.Modal.getOrCreateInstance(holder) : null; };
    return {
      el: holder,
      body: holder.querySelector('.modal-body'),
      footer: holder.querySelector('.modal-footer'),
      open: function () { const m = bs(); if (m) m.show(); else { holder.classList.add('show'); holder.style.display = 'block'; document.body.classList.add('modal-open'); } },
      close: function () {
        const m = bs();
        if (m) m.hide();
        else { holder.classList.remove('show'); holder.style.display = 'none'; document.body.classList.remove('modal-open'); }
      },
      onClose: function (cb) { holder.addEventListener('hidden.bs.modal', cb); },
      destroy: function () { setTimeout(function () { holder.remove(); }, 350); }
    };
  }

  function promptFields(title, fields, opts) {
    opts = opts || {};
    return new Promise(function (resolve) {
      const mod = ensureModal(document.body, title);
      let done = false;
      const finish = function (val) { if (!done) { done = true; mod.close(); mod.destroy(); resolve(val); } };
      mod.onClose(function () { finish(null); });
      mod.body.replaceChildren();
      mod.footer.replaceChildren();
      // O form precisa de id para o botão do footer: ele fica FORA da tag
      // <form> (footer do modal) e só submete via atributo "form".
      const formId = 'pf-form-' + (++modalSeq);
      const form = el('form', { id: formId });
      const inpIds = {};
      fields.forEach(function (f) {
        const inpId = 'pf-in-' + (++modalSeq) + '-' + f.name;
        inpIds[f.name] = inpId;
        const inputCls = f.type === 'select' ? 'form-select' : 'form-control';
        let input;
        if (f.type === 'textarea') {
          input = el('textarea', { id: inpId, class: inputCls, rows: f.rows || 3, placeholder: f.placeholder || null, required: f.required ? true : null });
        } else if (f.type === 'select') {
          const options = (f.options || []).map(function (o) {
            const val = (typeof o === 'object' && o !== null) ? (o.value === undefined || o.value === null ? '' : String(o.value)) : String(o);
            const lab = (typeof o === 'object' && o !== null) ? (o.label !== undefined && o.label !== null ? o.label : val) : val;
            return el('option', { value: val }, lab);
          });
          input = el('select', { id: inpId, class: inputCls, required: f.required ? true : null }, options);
          if (f.value !== null && f.value !== undefined && f.value !== '') input.value = String(f.value);
        } else {
          input = el('input', {
            id: inpId, type: f.type || 'text', class: inputCls,
            placeholder: f.placeholder || null,
            step: f.step || null, min: f.min === undefined ? null : String(f.min), max: f.max === undefined ? null : String(f.max),
            required: f.required ? true : null
          });
          if (f.value !== null && f.value !== undefined && f.value !== '') input.value = String(f.value);
        }
        const wrap = el('div', { class: 'mb-3' },
          el('label', { class: 'form-label', for: inpId }, f.label + (f.required ? ' *' : '')),
          input);
        if (f.hint) wrap.appendChild(el('div', { class: 'form-text' }, f.hint));
        form.appendChild(wrap);
      });
      mod.body.appendChild(form);
      mod.footer.append(
        el('button', { type: 'button', class: 'btn btn-outline-secondary', 'data-bs-dismiss': 'modal' }, 'Cancelar'),
        el('button', { type: 'submit', form: formId, class: 'btn ' + (opts.primaryClass || 'btn-primary') }, opts.submitLabel || 'Salvar')
      );
      form.addEventListener('submit', function (ev) {
        ev.preventDefault();
        if (!form.checkValidity()) { form.reportValidity(); return; }
        const out = {};
        fields.forEach(function (f) {
          const node = form.querySelector('[id="' + inpIds[f.name] + '"]');
          if (!node) return;
          let v = node.value;
          if (f.type === 'number') v = (v === '' || v === null) ? null : Number(v);
          else if (f.type === 'text' || f.type === 'textarea') v = String(v).trim();
          out[f.name] = v;
        });
        finish(out);
      });
      mod.open();
      setTimeout(function () { const first = form.querySelector('input,select,textarea'); if (first) first.focus(); }, 140);
    });
  }

  function confirmDialog(title, message, opts) {
    opts = opts || {};
    return new Promise(function (resolve) {
      const mod = ensureModal(document.body, title);
      let done = false;
      const finish = function (val) { if (!done) { done = true; mod.close(); mod.destroy(); resolve(val); } };
      mod.onClose(function () { finish(false); });
      mod.body.replaceChildren(el('p', { class: 'mb-0' }, message));
      mod.footer.replaceChildren();
      const ok = el('button', { type: 'button', class: 'btn ' + (opts.danger ? 'btn-danger' : 'btn-primary') }, opts.confirmLabel || 'Confirmar');
      ok.addEventListener('click', function () { finish(true); });
      mod.footer.append(el('button', { type: 'button', class: 'btn btn-outline-secondary', 'data-bs-dismiss': 'modal' }, 'Cancelar'), ok);
      mod.open();
      setTimeout(function () { ok.focus(); }, 140);
    });
  }

  /* ============================ grade (tabela base) ============================ */

  function parts(root) {
    return { root: root, body: root.querySelector('[data-grid]'), kpi: root.querySelector('[data-kpi]'), cardsHost: root.querySelector('[data-cards]') };
  }

  function skeletonRow(body, cols) {
    body.replaceChildren(el('tr', {}, el('td', { colspan: String(cols) }, el('div', { class: 'loading-skeleton', 'aria-label': 'Carregando' }))));
  }

  function emptyRow(body, cols, msg) {
    body.replaceChildren(el('tr', {}, el('td', { colspan: String(cols) }, el('div', { class: 'empty-state', role: 'status' }, msg || 'Nenhum registro para o filtro atual.'))));
  }

  function errorRow(body, cols, msg) {
    body.replaceChildren(el('tr', {}, el('td', { colspan: String(cols) }, el('div', { class: 'empty-state text-danger', role: 'alert' }, 'Falha ao carregar: ', msg))));
  }

  function renderRows(body, cols, rows, emptyMsg) {
    if (!rows.length) { emptyRow(body, cols, emptyMsg); return; }
    body.replaceChildren(...rows);
  }

  function wireSearch(root, onTerm) {
    const input = root.querySelector('[data-search]');
    const go = root.querySelector('[data-search-go]');
    const clear = root.querySelector('[data-search-clear]');
    const apply = function () { onTerm(input ? input.value.trim() : ''); };
    if (input) input.addEventListener('keydown', function (ev) { if (ev.key === 'Enter') { ev.preventDefault(); apply(); } });
    if (go) go.addEventListener('click', apply);
    if (clear) clear.addEventListener('click', function () { if (input) input.value = ''; apply(); });
  }

  /* ============================ painéis de resultado ============================ */

  function resultsSlot(root) {
    let slot = root.querySelector('[data-results]');
    if (!slot) {
      slot = el('div', { 'data-results': 'true', 'aria-live': 'polite', class: 'mt-3' });
      root.appendChild(slot);
    }
    return slot;
  }

  function showResult(slot, opts) {
    opts = opts || {};
    slot.replaceChildren();
    const tone = opts.tone || 'success';
    const box = el('div', { class: 'alert alert-' + tone + ' border-start border-4 p-3', role: 'status' },
      el('div', { class: 'd-flex align-items-center gap-2 mb-1' },
        el('strong', {}, opts.title || 'Resultado da operação'),
        el('button', { type: 'button', class: 'btn-close ms-auto', 'aria-label': 'Dispensar resultado', onclick: function () { slot.replaceChildren(); } })
      ),
      el('div', { class: 'row g-3' }, (opts.nodes || []).map(function (n) { return el('div', { class: 'col-12' }, n); })));
    slot.appendChild(box);
    setTimeout(function () { box.scrollIntoView({ block: 'nearest' }); }, 60);
  }

  function kvTable(rows) {
    return el('table', { class: 'table table-sm table-bordered align-middle mb-0 w-100' },
      el('tbody', {}, rows.map(function (r) {
        return el('tr', {},
          el('th', { scope: 'row', class: 'text-start fw-semibold bg-light', style: 'width:38%' }, r[0]),
          el('td', {}, typeof r[1] === 'string' || typeof r[1] === 'number' ? r[1] : r[1]));
      })));
  }

  function boolMark(b) { return b ? '●' : '—'; }

  // Memória por dia (§4): tabela completa dos minutos calculados por dia.
  function memoriaTable(mem) {
    const head = ['Data', 'Util', 'Feriado', 'Sem escala', 'Falta', 'Aus. just.', 'Esperado', 'Trabalhado', 'Intervalo', 'Atraso', 'Hora extra', 'Entrada', 'Saída', 'Pendências'];
    return el('div', { class: 'responsive-data-table' },
      el('table', { class: 'table table-sm table-hover align-middle' },
        el('thead', {}, el('tr', {}, head.map(function (h) { return el('th', { class: 'text-start', scope: 'col' }, h); }))),
        el('tbody', {}, (mem || []).map(function (d) {
          const pend = Array.isArray(d.pendencias) && d.pendencias.length
            ? el('span', { title: d.pendencias.join(', ') }, d.pendencias.length + ' pendência(s): ' + d.pendencias.join(', '))
            : '—';
          return el('tr', {},
            el('td', {}, fmtDate(d.data)),
            el('td', { title: 'Dia útil?' }, boolMark(d.diaUtil)),
            el('td', { title: 'Feriado?' }, boolMark(d.feriado)),
            el('td', { title: 'Sem escala no dia?' }, boolMark(d.semEscala)),
            el('td', { title: 'Dia de falta?' }, boolMark(d.diaFalta)),
            el('td', { title: 'Ausência justificada?' }, boolMark(d.ausenciaJustificada)),
            el('td', {}, fmtMin(d.esperadoMinutos)),
            el('td', {}, fmtMin(d.trabalhadoMinutos)),
            el('td', {}, fmtMin(d.intervaloMinutos)),
            el('td', {}, fmtMin(d.atrasoMinutos)),
            el('td', {}, fmtMin(d.horaExtraMinutos)),
            el('td', {}, fmtTime(d.entradaEfetiva)),
            el('td', {}, fmtTime(d.saidaEfetiva)),
            el('td', {}, pend));
        }))))
      ;
  }

  function lancTable(lancs) {
    return el('div', { class: 'responsive-data-table' },
      el('table', { class: 'table table-sm table-hover align-middle' },
        el('thead', {}, el('tr', {},
          ['Rubrica', 'Descrição', 'Tipo', 'Base', 'Quant.', 'Valor'].map(function (h) { return el('th', { class: 'text-start', scope: 'col' }, h); })
        )),
        el('tbody', {}, (lancs || []).map(function (l) {
          return el('tr', {},
            el('td', {}, l.rubricaCodigo || '—'),
            el('td', {}, l.rubricaNome || '—'),
            el('td', {}, chip(String(l.tipo || ''), String(l.tipo || '').toUpperCase() === 'DESCONTO' ? 'danger' : 'ok')),
            el('td', {}, l.base || '—'),
            el('td', {}, num(l.quantidadeBase)),
            el('td', {}, money(l.valor)));
        }))))
      ;
  }

  /* ============================ telas ============================ */

  // ---------- Dashboard do ponto ----------
  function initDashboard(root) {
    const p = parts(root);
    const COLS = 8;
    const kpis = {};
    (root.querySelectorAll('[data-kpi-status]').length ? root.querySelectorAll('[data-kpi-status]') : []).forEach(function (k) {
      kpis[k.dataset.kpiStatus] = k;
    });
    if (!Object.keys(kpis).length && p.kpi) kpis['__total'] = p.kpi;

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/apuracoes', { pageSize: 200 })
        .then(function (r) {
          const itens = r.items;
          if (kpis['__total']) kpis['__total'].textContent = String(itens.length);
          const count = function (st) { return itens.filter(function (x) { return String(pick(x.dados || {}, 'status') || '').toUpperCase() === st; }).length; };
          const comPend = itens.filter(function (x) { const pg = pick(x.dados || {}, 'pendenciasGlobais'); return Array.isArray(pg) && pg.length > 0; }).length;
          if (kpis['APURADA']) kpis['APURADA'].textContent = String(count('APURADA'));
          if (kpis['HOMOLOGADA']) kpis['HOMOLOGADA'].textContent = String(count('HOMOLOGADA'));
          if (kpis['PENDENCIAS']) kpis['PENDENCIAS'].textContent = String(comPend);
          renderRows(p.body, COLS, itens.map(function (x) {
            const d = x.dados || {};
            const pg = pick(d, 'pendenciasGlobais');
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'servidorId', 'ServidorId') || '—')),
              el('td', {}, fmtDate(pick(d, 'periodoInicio', 'PeriodoInicio')) + ' – ' + fmtDate(pick(d, 'periodoFim', 'PeriodoFim'))),
              el('td', {}, pick(d, 'versaoRegras', 'VersaoRegras') || '—'),
              el('td', {}, fmtMin(pick(d, 'totalTrabalhadoMinutos', 'TotalTrabalhadoMinutos'))),
              el('td', {}, pg && pg.length ? chip(pg.length + ' pendência(s)', 'warning') : '—'),
              el('td', {}, statusChip(pick(d, 'status', 'Status') || '—')),
              el('td', {}, el('a', { class: 'btn btn-sm btn-outline-primary', href: '/RH/PontoApuracao?id=' + x.id }, 'Abrir')));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar o painel de ponto.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }
    const goBtn = root.querySelector('[data-action="recarregar"]');
    if (goBtn) goBtn.addEventListener('click', load);
    load();
  }

  // ---------- Jornadas (CRUD) ----------
  function initJornadas(root) {
    const p = parts(root);
    const COLS = 8;
    let termo = '';

    function carga(v) { return v === undefined || v === null ? '—' : num(v) + ' h/sem'; }

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/jornadas', { termo: termo })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, pick(d, 'Nome', 'nome') || '—'),
              el('td', {}, carga(pick(d, 'CargaHoraria', 'cargaHoraria'))),
              el('td', {}, fmtTime(pick(d, 'Entrada', 'entrada')) + ' – ' + fmtTime(pick(d, 'Saida', 'saida'))),
              el('td', {}, (pick(d, 'ToleranciaMinutos', 'toleranciaMinutos') === undefined || pick(d, 'ToleranciaMinutos', 'toleranciaMinutos') === null ? 0 : pick(d, 'ToleranciaMinutos', 'toleranciaMinutos')) + ' min'),
              el('td', {}, diasLabel(pick(d, 'DiasSemana', 'diasSemana'))),
              el('td', {}, statusChip(pick(d, 'Status', 'status') || (x.ativo ? 'ATIVO' : 'INATIVO'))),
              el('td', {}, actBtn('Editar', 'btn-outline-primary', function () { abrirFormulario(x); })));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar jornadas.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    function abrirFormulario(row) {
      const d = row ? (row.dados || {}) : {};
      promptFields(row ? 'Editar jornada #' + row.id : 'Nova jornada', [
        { name: 'nome', label: 'Nome da jornada', type: 'text', required: true, value: pick(d, 'Nome', 'nome') || '', placeholder: 'Ex.: Administrativo 40h' },
        { name: 'cargaHoraria', label: 'Carga horária semanal (horas)', type: 'number', required: true, step: '0.5', min: 1, max: 80, value: pick(d, 'CargaHoraria', 'cargaHoraria') || '' },
        { name: 'entrada', label: 'Horário de entrada', type: 'time', required: true, value: fmtTime(pick(d, 'Entrada', 'entrada')) },
        { name: 'saida', label: 'Horário de saída', type: 'time', required: true, value: fmtTime(pick(d, 'Saida', 'saida')) },
        { name: 'toleranciaMinutos', label: 'Tolerância de atraso (minutos)', type: 'number', value: pick(d, 'ToleranciaMinutos', 'toleranciaMinutos') === null ? 0 : (pick(d, 'ToleranciaMinutos', 'toleranciaMinutos') || 0), min: 0, max: 240 },
        { name: 'diasSemana', label: 'Dias de trabalho (ISO: 1=Segunda … 7=Domingo)', type: 'text', required: true, value: pick(d, 'DiasSemana', 'diasSemana') || '1,2,3,4,5', hint: 'Separe por vírgula. Ex.: 1,2,3,4,5 (segunda a sexta). Não invente regras por esfera — o dia útil vem daqui.' }
      ], { submitLabel: row ? 'Salvar alterações' : 'Criar jornada' })
        .then(function (v) {
          if (!v) return;
          const payload = {
            nome: v.nome,
            cargaHoraria: Number(v.cargaHoraria),
            entrada: v.entrada,
            saida: v.saida,
            toleranciaMinutos: Number(v.toleranciaMinutos || 0),
            diasSemana: v.diasSemana
          };
          const reqP = row
            ? req('/api/rh/ponto/jornadas/' + row.id, { method: 'PUT', body: JSON.stringify(payload) }).then(function () { toast('Jornada #' + row.id + ' atualizada.'); })
            : req('/api/rh/ponto/jornadas', { method: 'POST', body: JSON.stringify(payload) }).then(function (env) { toast('Jornada criada (id ' + env.data + ').'); });
          reqP.then(load).catch(function (e) { toast(normError(e, 'Não foi possível salvar a jornada.').message, 'error'); });
        });
    }

    const novo = root.querySelector('[data-action="novo"]');
    if (novo) novo.addEventListener('click', function () { abrirFormulario(null); });
    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Escalas ----------
  function initEscalas(root) {
    const p = parts(root);
    const COLS = 7;
    let termo = '';
    let jornadasMap = {};

    fetchList('/api/rh/ponto/jornadas', { pageSize: 200 })
      .then(function (r) {
        r.items.forEach(function (x) {
          const nome = pick(x.dados || {}, 'Nome', 'nome');
          if (nome) jornadasMap[x.id] = nome;
        });
      })
      .catch(function () { /* mapa opcional: escala segue funcionando com id */ });

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/escalas', { termo: termo })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            const st = String(pick(d, 'Status', 'status') || (x.ativo ? 'ATIVO' : 'INATIVO')).toUpperCase();
            const inativa = ['INATIVA', 'CANCELADA', 'EXPIRADA', 'ENCERRADA'].includes(st);
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'ServidorId', 'servidorId') || '—')),
              el('td', {}, 'Jornada ' + (pick(d, 'JornadaId', 'jornadaId') || '—') + (jornadasMap[pick(d, 'JornadaId', 'jornadaId')] ? ' (' + jornadasMap[pick(d, 'JornadaId', 'jornadaId')] + ')' : '')),
              el('td', {}, fmtDate(pick(d, 'PeriodoInicio', 'periodoInicio'))),
              el('td', {}, fmtDate(pick(d, 'PeriodoFim', 'periodoFim'))),
              el('td', {}, statusChip(st)),
              el('td', {}, actBtn('Inativar', 'btn-outline-danger', function () { inativar(x, st); }, { disabled: inativa })));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar escalas.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    function inativar(x, st) {
      confirmDialog('Inativar escala #' + x.id, 'A escala deixa de ser considerada nas apurações a partir deste ponto (histórico preservado). Status atual: ' + st + '. Deseja inativar?', { confirmLabel: 'Inativar' })
        .then(function (ok) {
          if (!ok) return;
          req('/api/rh/ponto/escalas/' + x.id + '/inativar', { method: 'POST', body: '{}' })
            .then(function () { toast('Escala #' + x.id + ' inativada.'); load(); })
            .catch(function (e) { toast(normError(e, 'Não foi possível inativar a escala.').message, 'error'); });
        });
    }

    function abrirFormulario() {
      const campos = [
        { name: 'servidorId', label: 'Servidor (id)', type: 'number', required: true, min: 1, placeholder: 'Ex.: 1' },
        jornadaField(),
        { name: 'inicio', label: 'Início da escala', type: 'date', required: true },
        { name: 'fim', label: 'Fim da escala (opcional)', type: 'date' }
      ];
      promptFields('Nova escala', campos, { submitLabel: 'Criar escala' })
        .then(function (v) {
          if (!v) return;
          const payload = { servidorId: Number(v.servidorId), jornadaId: Number(v.jornadaId), periodoInicio: v.inicio, periodoFim: v.fim || null };
          req('/api/rh/ponto/escalas', { method: 'POST', body: JSON.stringify(payload) })
            .then(function (env) { toast('Escala criada (id ' + env.data + ') para o servidor ' + payload.servidorId + '.'); load(); })
            .catch(function (e) { toast(normError(e, 'Não foi possível criar a escala.').message, 'error'); });
        });
    }

    function jornadaField() {
      const opts = Object.keys(jornadasMap).map(function (id) { return { value: id, label: id + ' — ' + jornadasMap[id] }; });
      if (opts.length) return { name: 'jornadaId', label: 'Jornada vinculada', type: 'select', required: true, options: opts };
      return { name: 'jornadaId', label: 'Jornada (id)', type: 'number', required: true, min: 1, hint: 'Crie antes uma jornada na tela RH > Ponto > Jornadas.' };
    }

    const novo = root.querySelector('[data-action="novo"]');
    if (novo) novo.addEventListener('click', abrirFormulario);
    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Registros (batidas + ajustes) ----------
  function initRegistros(root) {
    const p = parts(root);
    const COLS = 7;
    const slot = resultsSlot(root);
    let termo = '';
    const TIPOS = [
      { value: 'Entrada', label: 'Entrada' },
      { value: 'Saida', label: 'Saída' },
      { value: 'IntervaloInicio', label: 'Início do intervalo' },
      { value: 'IntervaloFim', label: 'Fim do intervalo' },
      { value: 'Ajuste', label: 'Ajuste' }
    ];

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/registros', { termo: termo })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            const ajustadoEm = pick(d, 'ajustadoEm', 'AjustadoEm');
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'ServidorId', 'servidorId') || '—')),
              el('td', {}, fmtDateTime(pick(d, 'DataHora', 'dataHora'))),
              el('td', {}, chip(tipoBatidaLabel(pick(d, 'Tipo', 'tipo')), 'info')),
              el('td', {}, pick(d, 'Origem', 'origem') || 'MANUAL'),
              el('td', {}, ajustadoEm ? [chip('AJUSTADO', 'warning'), el('br'), el('span', { class: 'small text-muted' }, 'em ' + fmtDateTime(ajustadoEm))] : (pick(d, 'Justificativa', 'justificativa') || '—')),
              el('td', {}, actBtn('Ajustar', 'btn-outline-primary', function () { ajustar(x); })));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar os registros de ponto.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    function agoraLocal() {
      const d = new Date();
      d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
      return d.toISOString().slice(0, 16);
    }

    function registrar() {
      promptFields('Registrar batida', [
        { name: 'servidorId', label: 'Servidor (id)', type: 'number', required: true, min: 1 },
        { name: 'dataHora', label: 'Data e hora da batida', type: 'datetime-local', required: true, value: agoraLocal() },
        { name: 'tipo', label: 'Tipo de batida', type: 'select', required: true, options: TIPOS, value: 'Entrada' },
        { name: 'origem', label: 'Origem', type: 'text', value: 'MANUAL', placeholder: 'MANUAL, BIOMETRICO, APP…' },
        { name: 'justificativa', label: 'Justificativa (obrigatória para tipo Ajuste)', type: 'textarea' }
      ], { submitLabel: 'Registrar' })
        .then(function (v) {
          if (!v) return;
          if (String(v.tipo) === 'Ajuste' && !(v.justificativa || '').trim()) { toast('Informe a justificativa do ajuste.', 'warning'); return; }
          req('/api/rh/ponto/registros', { method: 'POST', body: JSON.stringify({
            servidorId: Number(v.servidorId),
            dataHora: v.dataHora,
            tipo: v.tipo,
            origem: (v.origem || 'MANUAL').trim(),
            justificativa: (v.justificativa || '').trim() || null
          }) })
            .then(function (env) { toast('Batida registrada (id ' + env.data + ').'); load(); })
            .catch(function (e) { toast(normError(e, 'Não foi possível registrar a batida.').message, 'error'); });
        });
    }

    function ajustar(x) {
      const d = x.dados || {};
      const dh = pick(d, 'DataHora', 'dataHora');
      const dhLocal = dh ? String(dh).replace(/\.(.*)$/, '').replace('Z', '').slice(0, 16) : '';
      promptFields('Ajustar batida #' + x.id, [
        { name: 'servidorId', label: 'Servidor (id)', type: 'number', required: true, min: 1, value: pick(d, 'ServidorId', 'servidorId') || '' },
        { name: 'dataHora', label: 'Novo data/hora da batida', type: 'datetime-local', required: true, value: dhLocal },
        { name: 'tipo', label: 'Tipo da batida', type: 'select', required: true, options: TIPOS, value: pick(d, 'Tipo', 'tipo') || 'Entrada' },
        { name: 'origem', label: 'Origem', type: 'text', value: pick(d, 'Origem', 'origem') || 'MANUAL' },
        { name: 'justificativa', label: 'Justificativa do ajuste *', type: 'textarea', required: true, hint: 'O original (quem, quando, valor anterior e origem) fica preservado no registro e na auditoria. Ajustes fora da janela de ±1 dia são recusados; apurações dependentes são invalidadas.' }
      ], { submitLabel: 'Aplicar ajuste' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/registros/' + x.id + '/ajustar', { method: 'POST', body: JSON.stringify({
            servidorId: Number(v.servidorId),
            dataHora: v.dataHora,
            tipo: v.tipo,
            origem: (v.origem || 'MANUAL').trim(),
            justificativa: (v.justificativa || '').trim()
          }) })
            .then(function (env) {
              const r = env.data || {};
              toast('Ajuste aplicado em ' + fmtDateTime(r.ajustadoEm) + '.', 'success');
              showResult(slot, {
                title: 'Ajuste da batida #' + (r.registroId || x.id),
                nodes: [kvTable([
                  ['Registrado em', fmtDateTime(r.ajustadoEm)],
                  ['Apurações dependentes invalidadas', num(r.apuracoesInvalidadas)],
                  ['Janela do registro', fmtDate(r.janelaInicio) + ' a ' + fmtDate(r.janelaFim)]
                ])]
              });
              load();
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível aplicar o ajuste.').message, 'error'); });
        });
    }

    const novo = root.querySelector('[data-action="novo"]');
    if (novo) novo.addEventListener('click', registrar);
    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Justificativas ----------
  function initJustificativas(root) {
    const p = parts(root);
    const COLS = 6;
    const slot = resultsSlot(root);
    let termo = '';

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/justificativas', { termo: termo })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            const st = String(pick(d, 'Status', 'status') || 'PENDENTE').toUpperCase();
            const decidido = st === 'APROVADA' || st === 'REPROVADA';
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'ServidorId', 'servidorId') || '—')),
              el('td', {}, fmtDate(pick(d, 'DataReferencia', 'dataReferencia'))),
              el('td', {}, pick(d, 'Motivo', 'motivo') || '—'),
              el('td', {}, statusChip(st === 'PENDENTE' && x.ativo ? 'ANALISE' : st)),
              el('td', {},
                actBtn('Aprovar', 'btn-outline-success', function () { decidir(x, 'aprovar'); }, { disabled: decidido }),
                ' ',
                actBtn('Reprovar', 'btn-outline-danger', function () { decidir(x, 'reprovar'); }, { disabled: decidido })));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar as justificativas.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    function decidir(x, decisao) {
      confirmDialog(decisao === 'aprovar' ? 'Aprovar justificativa #' + x.id : 'Reprovar justificativa #' + x.id,
        'A decisão invalida as apurações dependentes do período (o recálculo passa a considerar a justificativa). ' +
        'O servidor não pode decidir sobre a própria justificativa (bloqueio explícito no backend).',
        { confirmLabel: decisao === 'aprovar' ? 'Aprovar' : 'Reprovar', danger: decisao === 'reprovar' })
        .then(function (ok) {
          if (!ok) return;
          req('/api/rh/ponto/justificativas/' + x.id + '/' + decisao, { method: 'POST', body: '{}' })
            .then(function (env) {
              const r = env.data || {};
              toast('Justificativa #' + (r.justificativaId || x.id) + ' ' + String(r.status || decisao).toLowerCase() + '.', 'success');
              showResult(slot, {
                title: 'Decisão da justificativa #' + (r.justificativaId || x.id),
                nodes: [kvTable([
                  ['Status', r.status || '—'],
                  ['Decidido por (usuário)', r.decididoPor === null || r.decididoPor === undefined ? '—' : '#' + r.decididoPor],
                  ['Apurações dependentes invalidadas', num(r.apuracoesInvalidadas)]
                ])]
              });
              load();
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível decidir a justificativa.').message, 'error'); });
        });
    }

    function nova() {
      promptFields('Nova justificativa de ponto', [
        { name: 'servidorId', label: 'Servidor (id)', type: 'number', required: true, min: 1 },
        { name: 'dataReferencia', label: 'Data de referência', type: 'date', required: true },
        { name: 'motivo', label: 'Motivo', type: 'textarea', required: true, rows: 4, placeholder: 'Descreva o ocorrido (ausência, atraso, esquecimento de batida…)' }
      ], { submitLabel: 'Enviar para análise' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/justificativas', { method: 'POST', body: JSON.stringify({
            servidorId: Number(v.servidorId),
            dataReferencia: v.dataReferencia,
            motivo: v.motivo
          }) })
            .then(function (env) { toast('Justificativa criada (id ' + env.data + ') e aguardando análise.'); load(); })
            .catch(function (e) { toast(normError(e, 'Não foi possível criar a justificativa.').message, 'error'); });
        });
    }

    const novo = root.querySelector('[data-action="novo"]');
    if (novo) novo.addEventListener('click', nova);
    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Apuração e homologação (núcleo) ----------
  function initApuracao(root) {
    const p = parts(root);
    const COLS = 9;
    const slot = resultsSlot(root);
    let termo = '';
    let detalheAtual = null;

    const detalheCard = el('section', { class: 'card mt-3', 'aria-label': 'Detalhe da apuração selecionada', style: 'background:#fff;' },
      el('div', { class: 'card-body', 'data-detalhe': 'true' },
        el('div', { class: 'empty-state' }, 'Selecione uma apuração na tabela para ver a memória por dia, os totais e as ações de homologação/reabertura/integração.')));
    root.appendChild(detalheCard);
    const detalheBody = detalheCard.querySelector('[data-detalhe]');

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/apuracoes', { termo: termo })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            const pg = pick(d, 'pendenciasGlobais');
            const ativo = detalheAtual && detalheAtual.id === x.id;
            const tr = el('tr', { class: ativo ? 'table-active' : null },
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'servidorId', 'ServidorId') || '—')),
              el('td', {}, fmtDate(pick(d, 'periodoInicio', 'PeriodoInicio')) + ' – ' + fmtDate(pick(d, 'periodoFim', 'PeriodoFim'))),
              el('td', {}, pick(d, 'versaoRegras', 'VersaoRegras') || '—'),
              el('td', {}, num(pick(d, 'reprocessamentos', 'Reprocessamentos'))),
              el('td', {}, fmtMin(pick(d, 'totalTrabalhadoMinutos', 'TotalTrabalhadoMinutos'))),
              el('td', {}, Array.isArray(pg) && pg.length ? chip(pg.length + ' pend.', 'warning') : '—'),
              el('td', {}, statusChip(pick(d, 'status', 'Status') || '—')),
              el('td', {}, actBtn('Detalhar', ativo ? 'btn-primary' : 'btn-outline-primary', function () { selecionar(x.id); })));
            return tr;
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar as apurações.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    function selecionar(id) {
      req('/api/rh/ponto/apuracoes/' + id)
        .then(function (env) {
          detalheAtual = env.data || null;
          renderDetalhe(detalheAtual);
          load();
        })
        .catch(function (e) { toast(normError(e, 'Não foi possível carregar a apuração.').message, 'error'); });
    }

    function kpiMini(items) {
      return el('div', { class: 'row g-2 mb-2' }, items.map(function (it) {
        return el('div', { class: 'col-6 col-md-2' },
          el('div', { class: 'border rounded p-2 text-center', style: 'background:#f8fafc;' },
            el('div', { class: 'fs-5 fw-bold' }, String(it[1] === null || it[1] === undefined ? '—' : it[1])),
            el('small', { class: 'text-muted d-block' }, it[0])));
      }));
    }

    function renderDetalhe(reg) {
      const d = (reg && reg.dados) || {};
      const st = String(pick(d, 'status') || '—').toUpperCase();
      detalheBody.replaceChildren(
        el('div', { class: 'd-flex flex-wrap align-items-center gap-2 mb-2' },
          el('strong', {}, 'Apuração #' + reg.id),
          statusChip(st),
          chip(pick(d, 'versaoRegras') || 'sem versão', 'info'),
          el('span', { class: 'text-muted' }, 'servidor ' + (pick(d, 'servidorId') || '—') + ' • ' + fmtDate(pick(d, 'periodoInicio')) + ' a ' + fmtDate(pick(d, 'periodoFim'))),
          el('span', { class: 'ms-auto text-muted small' }, 'calculado em ' + fmtDateTime(pick(d, 'calculadoEm')) + ' • fuso ' + (pick(d, 'fusoHorarioOperacao') || '—') + ' • reprocessamentos ' + (pick(d, 'reprocessamentos') || 0))
        ),
        kpiMini([
          ['Dias úteis previstos', pick(d, 'diasUteisPrevistos')],
          ['Dias sem escala', pick(d, 'diasSemEscala')],
          ['Dias de falta', pick(d, 'diasFalta')],
          ['Ausências justificadas', pick(d, 'diasAusenciaJustificada')],
          ['Trabalhado', fmtMin(pick(d, 'totalTrabalhadoMinutos'))],
          ['Atraso', fmtMin(pick(d, 'totalAtrasoMinutos'))]
        ]),
        el('div', { class: 'row g-2 mb-2' }, [
          ['Intervalo total', fmtMin(pick(d, 'totalIntervaloMinutos'))],
          ['Ausência total', fmtMin(pick(d, 'totalAusenciaMinutos'))],
          ['Hora extra', fmtMin(pick(d, 'totalHoraExtraMinutos'))]
        ].map(function (it) {
          return el('div', { class: 'col-6 col-md-2' },
            el('div', { class: 'border rounded p-2 text-center', style: 'background:#f8fafc;' },
              el('div', { class: 'fs-6 fw-bold' }, it[1]),
              el('small', { class: 'text-muted d-block' }, it[0])));
        })),
        (() => {
          const res = pick(d, 'resumo');
          if (res && typeof res === 'object') {
            const RESUMO_LABELS = {
              totalTrabalhadoFormatado: 'Total trabalhado (formatado)',
              totalAtrasoFormatado: 'Total de atraso (formatado)',
              totalAusenciaFormatado: 'Total de ausência (formatado)',
              totalHoraExtraFormatado: 'Total de hora extra (formatado)'
            };
            const pairs = [];
            Object.keys(res).forEach(function (k) { pairs.push([RESUMO_LABELS[k] || k, res[k]]); });
            return el('div', { class: 'mb-2' }, kvTable(pairs));
          }
          return el('div', { class: 'mb-2 text-muted small' }, 'Resumo formatado ausente.');
        })(),
        (() => {
          const pg = pick(d, 'pendenciasGlobais');
          if (Array.isArray(pg) && pg.length) {
            return el('div', { class: 'alert alert-warning py-2 mb-2 d-flex flex-wrap gap-2 align-items-center' },
              el('strong', {}, 'Pendências globais:'),
              pg.map(function (x) { return chip(x, 'warning'); }));
          }
          return el('div', { class: 'mb-2 text-success small' }, 'Sem pendências globais no período.');
        })(),
        el('h6', { class: 'mt-3 mb-2' }, 'Memória por dia (minutos calculados pela engine v' + (pick(d, 'versaoRegras') || '') + ')'),
        Array.isArray(pick(d, 'memoriaPorDia'))
          ? memoriaTable(pick(d, 'memoriaPorDia'))
          : el('div', { class: 'alert alert-danger' }, 'Memória por dia ausente — a homologação será recusada (requisito explícito, sem sucesso simulado).'),
        el('div', { class: 'd-flex flex-wrap gap-2 mt-3 border-top pt-3' }, actionBar(reg, st))
      );
      setTimeout(function () { detalheCard.scrollIntoView({ block: 'nearest' }); }, 80);
    }

    function actionBar(reg, st) {
      const buttons = [];
      if (st === 'APURADA') buttons.push(actBtn('Homologar', 'btn-success', function () { homologar(reg); }, { title: 'congela a competência; exige memória e versão vigente' }));
      if (st === 'APURADA') buttons.push(actBtn('Recalcular período', 'btn-outline-secondary', function () { recalcular(reg); }, { title: 'reprocessa sem duplicar; incrementa o contador' }));
      if (st === 'HOMOLOGADA') buttons.push(actBtn('Integrar na folha', 'btn-primary', function () { integrar(reg); }, { title: 'materializa lançamentos na folha de destino' }));
      if (st === 'HOMOLOGADA') buttons.push(actBtn('Reabrir', 'btn-outline-warning', function () { reabrir(reg); }, { title: 'exige justificativa; preserva histórico' }));
      if (!buttons.length) buttons.push(el('span', { class: 'text-muted small align-self-center' }, 'Ações disponíveis conforme o status: APURADA permite homologar/recalcular; HOMOLOGADA permite integrar e reabrir.'));
      return buttons;
    }

    function homologar(reg) {
      promptFields('Homologar apuração #' + reg.id, [
        { name: 'divergencia', label: 'Justificativa da divergência (opcional)', type: 'textarea', hint: 'A homologação revalida a versão das regras e a memória por dia e congela a competência até uma reabertura formal.' }
      ], { submitLabel: 'Homologar', primaryClass: 'btn-success' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/apuracoes/' + reg.id + '/homologar', { method: 'POST', body: JSON.stringify({ justificativaDivergencia: (v.divergencia || '').trim() || null }) })
            .then(function (env) {
              const r = env.data || {};
              toast(r.jaHomologada ? 'Apuração já estava homologada — nenhuma transição nova foi gravada.' : 'Apuração #' + reg.id + ' homologada.', r.jaHomologada ? 'warning' : 'success');
              showResult(slot, {
                title: (r.jaHomologada ? 'Apuração já homologada #' : 'Homologação concluída — apuração #') + (r.apuracaoId || reg.id),
                tone: r.jaHomologada ? 'warning' : 'success',
                nodes: [kvTable([
                  ['Status', r.status || '—'],
                  ['Transição em', fmtDateTime(r.transicaoEm)],
                  ['Versão das regras revalidada', r.versaoRegras || '—'],
                  ['Pendências globais no ato da homologação', (r.pendenciasGlobais || []).length ? (r.pendenciasGlobais || []).join(', ') : 'nenhuma']
                ])]
              });
              selecionar(reg.id);
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível homologar.').message, 'error'); });
        });
    }

    function reabrir(reg) {
      promptFields('Reabrir apuração #' + reg.id, [
        { name: 'justificativa', label: 'Justificativa da reabertura *', type: 'textarea', required: true, hint: 'A reabertura volta a apuração para APURADA, preserva o histórico e bloqueia enquanto houver competência fechada/fechamento em curso.' }
      ], { submitLabel: 'Reabrir', primaryClass: 'btn-warning' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/apuracoes/' + reg.id + '/reabrir', { method: 'POST', body: JSON.stringify({ justificativa: (v.justificativa || '').trim() }) })
            .then(function (env) {
              const r = env.data || {};
              toast(r.jaReaberta ? 'Apuração já havia sido reaberta — sem nova transição.' : 'Apuração #' + reg.id + ' reaberta (status ' + (r.status || 'APURADA') + ').', r.jaReaberta ? 'warning' : 'success');
              showResult(slot, {
                title: (r.jaReaberta ? 'Reabertura já registrada — apuração #': 'Reabertura concluída — apuração #') + (r.apuracaoId || reg.id),
                tone: r.jaReaberta ? 'warning' : 'info',
                nodes: [kvTable([
                  ['Status', r.status || '—'],
                  ['Transição em', fmtDateTime(r.transicaoEm)],
                  ['Reaberto por (usuário)', r.reabertoPor === null || r.reabertoPor === undefined ? '—' : '#' + r.reabertoPor]
                ])]
              });
              selecionar(reg.id);
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível reabrir.').message, 'error'); });
        });
    }

    function recalcular(reg) {
      const d = reg.dados || {};
      confirmDialog('Recalcular período',
        'Reprocessa a apuração #' + reg.id + ' (' + fmtDate(pick(d, 'periodoInicio')) + ' a ' + fmtDate(pick(d, 'periodoFim')) + ') do servidor ' + (pick(d, 'servidorId') || '—') +
        '. Sem duplicação: a mesma apuração é atualizada e o contador de reprocessamentos é incrementado. Continuar?',
        { confirmLabel: 'Recalcular' })
        .then(function (ok) {
          if (!ok) return;
          req('/api/rh/ponto/apurar', { method: 'POST', body: JSON.stringify({
            servidorId: Number(pick(d, 'servidorId') || 0),
            periodoInicio: String(pick(d, 'periodoInicio')).slice(0, 10),
            periodoFim: String(pick(d, 'periodoFim')).slice(0, 10)
          }) })
            .then(function (env) {
              toast('Apuração recalculada (id ' + env.data + ').', 'success');
              selecionar(env.data || reg.id);
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível recalcular.').message, 'error'); });
        });
    }

    function integrar(reg) {
      promptFields('Integrar apuração #' + reg.id + ' na folha', [
        { name: 'folhaId', label: 'Folha de destino (id)', type: 'number', required: true, min: 1, hint: 'Apenas apuração HOMOLOGADA pode ser integrada. Os lançamentos são materializados com unicidade origem→destino (retry não duplica).' }
      ], { submitLabel: 'Integrar', primaryClass: 'btn-primary' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/apuracoes/' + reg.id + '/integrar-folha', { method: 'POST', body: JSON.stringify({ folhaId: Number(v.folhaId) }) })
            .then(function (env) {
              const r = env.data || {};
              toast('Apuração integrada na folha #' + r.folhaId + (r.jaProcessada ? ' (idempotente — sem lançamento duplicado)' : '') + '.', 'success');
              showResultIntegracao(slot, r);
              selecionar(reg.id);
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível integrar na folha.').message, 'error'); });
        });
    }

    function showResultIntegracao(target, r) {
      const fin = r.financeira || {};
      const finNodes = [kvTable(fin.habilitada ? [
        ['Habilitado no exercício', 'sim'],
        ['Chave de idempotência', fin.chaveIdempotencia || '—'],
        ['Evento na fila (outbox)', fin.eventoFilaId ? '#' + fin.eventoFilaId : '—'],
        ['Status da fila', fin.statusFila ? statusChip(fin.statusFila) : '—'],
        ['Tentativas', num(fin.tentativasFila)],
        ['Erro da fila', fin.erroFila || '—'],
        ['Processado em', fmtDateTime(fin.processadaEm)],
        ['Documento de empenho', fin.documentoEmpenhoId ? '#' + fin.documentoEmpenhoId + ' (emitido — sem liquidação nem pagamento automático)' : '—']
      ] : [['Habilitado no exercício', 'não — HABILITAR_INTEGRACAO_FINANCEIRA=false; nenhum evento de fila foi emitido']] )];
      showResult(target, {
        title: 'Integração da apuração #' + (r.apuracaoId || '—') + ' na folha #' + (r.folhaId || '—') + (r.jaProcessada ? ' (reprocessamento idempotente)' : ''),
        tone: 'success',
        nodes: [
          kvTable([
            ['Integração (id)', '#' + (r.integracaoId || '—')],
            ['Evento na folha', r.eventoFolhaId ? '#' + r.eventoFolhaId : '—'],
            ['Versão das regras usada', r.versaoRegras || '—'],
            ['Já processada anteriormente?', r.jaProcessada ? 'sim — nenhum lançamento novo foi criado' : 'não']
          ]),
          el('h6', { class: 'mb-1' }, 'Lançamentos materializados'),
          (r.lancamentos && r.lancamentos.length) ? lancTable(r.lancamentos) : el('div', { class: 'text-muted small' }, 'Sem lançamentos retornados.'),
          el('div', { class: 'd-flex flex-wrap gap-3 mt-2 fw-semibold' },
            el('span', {}, 'Proventos: ' + money(r.totalProventos)),
            el('span', {}, 'Descontos: ' + money(r.totalDescontos)),
            el('span', {}, 'Líquido: ' + money(r.liquido))),
          (r.criticasNaoBloqueantes && r.criticasNaoBloqueantes.length)
            ? el('div', { class: 'alert alert-warning py-2 mt-2' }, el('strong', {}, 'Críticas não bloqueantes:'), ' ' + (r.criticasNaoBloqueantes).join('; '))
            : null,
          el('h6', { class: 'mb-1 mt-2' }, 'Relação com Financeiro (§9)'),
          finNodes[0]
        ].filter(function (n) { return n !== null; })
      });
    }

    function abrirFormulario() {
      promptFields('Calcular apuração de ponto', [
        { name: 'servidorId', label: 'Servidor (id)', type: 'number', required: true, min: 1 },
        { name: 'periodoInicio', label: 'Início do período', type: 'date', required: true },
        { name: 'periodoFim', label: 'Fim do período', type: 'date', required: true, hint: 'O servidor precisa de escala ativa cobrindo o período (SEM_ESCALA caso contrário). Apuração homologada do mesmo período precisa ser reaberta antes.' }
      ], { submitLabel: 'Calcular', primaryClass: 'btn-primary' })
        .then(function (v) {
          if (!v) return;
          req('/api/rh/ponto/apurar', { method: 'POST', body: JSON.stringify({
            servidorId: Number(v.servidorId),
            periodoInicio: v.periodoInicio,
            periodoFim: v.periodoFim
          }) })
            .then(function (env) {
              toast('Apuração calculada (id ' + env.data + ').', 'success');
              load();
              selecionar(env.data);
            })
            .catch(function (e) { toast(normError(e, 'Não foi possível calcular a apuração.').message, 'error'); });
        });
    }

    const novo = root.querySelector('[data-action="novo"]');
    if (novo) novo.addEventListener('click', abrirFormulario);
    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', function () { load(); if (detalheAtual) selecionar(detalheAtual.id); });
    wireSearch(root, function (t) { termo = t; load(); });
    load();

    const paramId = new URLSearchParams(window.location.search).get('id');
    if (paramId && /^\d+$/.test(paramId)) selecionar(Number(paramId));
  }

  // ---------- Espelho de ponto ----------
  function initEspelho(root) {
    const p = parts(root);
    const COLS = 6;
    let termo = '';

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/ponto/espelho', { termo: termo, pageSize: 200 })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          const ordenado = r.items.slice().sort(function (a, b) {
            const ta = String(pick(a.dados || {}, 'DataHora', 'dataHora') || a.createdAt || '');
            const tb = String(pick(b.dados || {}, 'DataHora', 'dataHora') || b.createdAt || '');
            return tb.localeCompare(ta);
          });
          renderRows(p.body, COLS, ordenado.map(function (x) {
            const d = x.dados || {};
            return el('tr', {},
              el('td', {}, '#' + x.id),
              el('td', {}, 'Servidor ' + (pick(d, 'ServidorId', 'servidorId') || '—')),
              el('td', {}, fmtDateTime(pick(d, 'DataHora', 'dataHora') || x.createdAt)),
              el('td', {}, chip(tipoBatidaLabel(pick(d, 'Tipo', 'tipo')), 'info')),
              el('td', {}, pick(d, 'Origem', 'origem') || 'MANUAL'),
              el('td', {}, pick(d, 'Justificativa', 'justificativa') || '—'));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar o espelho de ponto.').message, 'error');
          errorRow(p.body, COLS, normError(e).message);
        });
    }

    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Portal: meu ponto ----------
  function initPortalPonto(root) {
    const p = parts(root);
    const COLS = 5;
    let termo = '';

    function load() {
      skeletonRow(p.body, COLS);
      fetchList('/api/rh/portal/ponto', { termo: termo, pageSize: 100 })
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.total);
          renderRows(p.body, COLS, r.items.map(function (x) {
            const d = x.dados || {};
            return el('tr', {},
              el('td', {}, fmtDateTime(pick(d, 'DataHora', 'dataHora') || x.createdAt)),
              el('td', {}, chip(tipoBatidaLabel(pick(d, 'Tipo', 'tipo')), 'info')),
              el('td', {}, pick(d, 'Origem', 'origem') || 'MANUAL'),
              el('td', {}, pick(d, 'Justificativa', 'justificativa') || '—'),
              el('td', {}, statusChip(pick(d, 'Status', 'status') || (x.ativo ? 'REGISTRADO' : 'INATIVO'))));
          }));
        })
        .catch(function (e) {
          toast(normError(e, 'Não foi possível carregar seu ponto.').message, 'error');
          emptyRow(p.body, COLS, normError(e).message);
        });
    }

    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    wireSearch(root, function (t) { termo = t; load(); });
    load();
  }

  // ---------- Portal: pendências ----------
  function initPendencias(root) {
    const p = parts(root);
    const host = p.cardsHost || p.body;

    function cards(itens) {
      if (!itens.length) return [el('div', { class: 'empty-state col-12 py-4', role: 'status' }, 'Nenhuma pendência ativa para o seu servidor. Bom trabalho!')];
      return itens.map(function (i) {
        return el('article', { class: 'sigov-pendency-card col-12 col-md-6 col-lg-4' },
          el('div', { class: 'd-flex align-items-center gap-2 mb-1' },
            el('strong', {}, i.tipo || 'Pendencia'),
            statusChip(i.status || 'PENDENTE'),
            el('span', { class: 'ms-auto text-muted small' }, '#' + i.id)),
          el('b', {}, i.descricao || 'Item aguardando tratamento.'),
          el('span', { class: 'd-block mt-1 text-muted small' }, 'registrado em ' + fmtDateTime(i.em)));
      });
    }

    function load() {
      host.replaceChildren(el('div', { class: 'loading-skeleton col-12', 'aria-label': 'Carregando' }));
      fetchList('/api/rh/portal/pendencias')
        .then(function (r) {
          if (p.kpi) p.kpi.textContent = String(r.items.length);
          host.replaceChildren(...cards(r.items));
        })
        .catch(function (e) {
          const msg = normError(e).message;
          toast(msg, 'error');
          host.replaceChildren(el('div', { class: 'empty-state text-danger col-12 py-4', role: 'alert' }, 'Falha ao carregar pendências: ', msg));
        });
    }

    const refresh = root.querySelector('[data-action="recarregar"]');
    if (refresh) refresh.addEventListener('click', load);
    load();
  }

  /* ============================ exportação pública ============================ */

  // As telas renderizam o corpo ANTES dos scripts do layout (sigov.api.js define
  // window.sigovApi no fim do body). Se o documento ainda está carregando, os
  // inits só executam no DOMContentLoaded — quando sigovApi já existe.
  function ready(fn) {
    if (document.readyState !== 'loading') { fn(); return; }
    document.addEventListener('DOMContentLoaded', fn, { once: true });
  }

  window.SigovRhPonto = {
    initDashboard: (root) => ready(() => initDashboard(root)),
    initJornadas: (root) => ready(() => initJornadas(root)),
    initEscalas: (root) => ready(() => initEscalas(root)),
    initRegistros: (root) => ready(() => initRegistros(root)),
    initJustificativas: (root) => ready(() => initJustificativas(root)),
    initApuracao: (root) => ready(() => initApuracao(root)),
    initEspelho: (root) => ready(() => initEspelho(root)),
    initPortalPonto: (root) => ready(() => initPortalPonto(root)),
    initPendencias: (root) => ready(() => initPendencias(root)),
    ui: {
      el: el, pick: pick, req: req, fetchList: fetchList, normError: normError, toast: toast,
      fmtDate: fmtDate, fmtDateTime: fmtDateTime, fmtTime: fmtTime, fmtMin: fmtMin, money: money, num: num,
      diasLabel: diasLabel, tipoBatidaLabel: tipoBatidaLabel, statusChip: statusChip, chip: chip, actBtn: actBtn,
      kvTable: kvTable, memoriaTable: memoriaTable, lancTable: lancTable,
      skeletonRow: skeletonRow, emptyRow: emptyRow, errorRow: errorRow, renderRows: renderRows,
      wireSearch: wireSearch, parts: parts, resultsSlot: resultsSlot, showResult: showResult, confirmDialog: confirmDialog
    }
  };
})();
