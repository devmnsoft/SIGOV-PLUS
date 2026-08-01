(function (window, $) {
  'use strict';
  class SigovModal {
    open(selector) {
      const element = document.querySelector(selector);
      if (element && window.bootstrap) window.bootstrap.Modal.getOrCreateInstance(element).show();
    }
  }
  class SigovConfirmModal {
    confirm(message) {
      if (!window.SigovConfirm) return Promise.resolve(false);
      return window.SigovConfirm.show({ message: message || 'Confirme antes de continuar.' });
    }
  }
  window.Sigov = window.Sigov || {};
  window.Sigov.modal = new SigovModal();
  window.Sigov.confirmModal = new SigovConfirmModal();
  window.Sigov.SigovModal = SigovModal;
  window.Sigov.SigovConfirmModal = SigovConfirmModal;
  $(document).on('click', '[data-sigov-confirm]', async function (event) {
    if (this.dataset.sigovConfirmed === 'true') { delete this.dataset.sigovConfirmed; return; }
    event.preventDefault();
    if (!await window.Sigov.confirmModal.confirm($(this).data('sigov-confirm'))) return;
    this.dataset.sigovConfirmed = 'true';
    if (this.form) this.form.requestSubmit(this); else this.click();
  });
})(window, window.jQuery);
