(function ($) {
  'use strict';
  var script = document.currentScript && document.currentScript.getAttribute('src') || '';
  var endpoint = 'dashboard';
  if (script.indexOf('consumidores') >= 0) endpoint = 'consumidores';
  if (script.indexOf('ligacoes') >= 0) endpoint = 'ligacoes';
  if (script.indexOf('unidades') >= 0) endpoint = 'unidades-consumidoras';
  if (script.indexOf('hidrometros') >= 0) endpoint = 'hidrometros';
  if (script.indexOf('leituras') >= 0) endpoint = 'leituras';
  if (script.indexOf('faturas') >= 0) endpoint = 'faturas';
  if (script.indexOf('ordens-servico') >= 0) endpoint = 'ordens-servico';
  if (script.indexOf('laboratorio') >= 0) endpoint = 'laboratorio/amostras';
  if (script.indexOf('rede') >= 0) endpoint = 'rede/trechos';
  $.getScript('/js/modules/saneamento.common.js').done(function () { window.sigovSaneamento.wire(endpoint); });
}(jQuery));
