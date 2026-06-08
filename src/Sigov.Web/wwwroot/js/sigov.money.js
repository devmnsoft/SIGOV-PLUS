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
  window.Sigov.money = { parse, format };
  $(document).on('blur', '[data-sigov-money]', function () { this.value = format(this.value); });
})(window, window.jQuery);
