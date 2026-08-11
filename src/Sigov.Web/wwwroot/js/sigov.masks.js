(function (window) {
  'use strict';
  const digits = value => String(value || '').replace(/\D/g, '');
  const formats = {
    cpf(value) { const v = digits(value).slice(0, 11); return v.replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d{1,2})$/, '$1-$2'); },
    cnpj(value) { const v = digits(value).slice(0, 14); return v.replace(/(\d{2})(\d)/, '$1.$2').replace(/(\d{3})(\d)/, '$1.$2').replace(/(\d{3})(\d)/, '$1/$2').replace(/(\d{4})(\d{1,2})$/, '$1-$2'); },
    cpfcnpj(value) { return digits(value).length <= 11 ? formats.cpf(value) : formats.cnpj(value); },
    phone(value) { const v = digits(value).slice(0, 11); return v.length > 10 ? v.replace(/(\d{2})(\d{5})(\d{0,4})/, '($1) $2-$3') : v.replace(/(\d{2})(\d{4})(\d{0,4})/, '($1) $2-$3'); },
    cep(value) { return digits(value).slice(0, 8).replace(/(\d{5})(\d{0,3})/, '$1-$2'); },
    money(value) { const cents = digits(value).padStart(3, '0'); return (Number(cents) / 100).toLocaleString('pt-BR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); },
    date(value) { return digits(value).slice(0, 8).replace(/(\d{2})(\d)/, '$1/$2').replace(/(\d{2})(\d)/, '$1/$2'); }
  };
  window.Sigov = window.Sigov || {};
  window.Sigov.masks = Object.assign({ digits }, formats);
  document.addEventListener('input', event => {
    const input = event.target.closest('[data-sigov-mask]');
    const formatter = input && formats[input.dataset.sigovMask.toLowerCase()];
    if (formatter) input.value = formatter(input.value);
  });
})(window);
