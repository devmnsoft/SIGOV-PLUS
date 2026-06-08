(function ($) {
  'use strict';
  function card(item) { return '<div class="col-sm-6 col-xl-3"><div class="card h-100 shadow-sm"><div class="card-body"><div class="text-muted small">' + (item.categoria || '') + '</div><div class="fs-3 fw-bold">' + (item.valor || 0) + '</div><div class="fw-semibold">' + (item.nome || item.codigo) + '</div><span class="badge bg-light text-dark mt-2">anonimizado/agregado</span></div></div></div>'; }
  $(function () { var $target = $('#agroBiCards'); if (!$target.length) return; $.getJSON('/api/agro/bi/dashboard').done(function (r) { var cards = (r.data && r.data.cards) || r.cards || []; $target.html(cards.length ? cards.map(card).join('') : '<div class="col-12"><div class="alert alert-secondary">Nenhum indicador disponível.</div></div>'); }).fail(function (xhr) { $target.html('<div class="col-12"><div class="alert alert-danger">Não foi possível carregar o BI Agro (' + xhr.status + ').</div></div>'); }); });
})(jQuery);
