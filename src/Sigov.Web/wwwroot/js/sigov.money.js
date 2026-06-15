(function (window, $) {
  window.Sigov = window.Sigov || {};
  function parse(value) {
    if (typeof value === 'number') return value;
    const text = String(value || '').replace(/\./g, '').replace(',', '.').replace(/[^0-9.-]/g, '');
    return Number(text || 0);
  }
  function format(value) {
    return parse(value).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' });
  }
  function formatMoneyField(event) {
    const target = event.target;
    if (target && target.matches && target.matches('[data-sigov-money]')) {
      target.value = format(target.value);
    }
  }
  window.Sigov.money = { parse, format };
  document.addEventListener('blur', formatMoneyField, true);
})(window, window.jQuery);
