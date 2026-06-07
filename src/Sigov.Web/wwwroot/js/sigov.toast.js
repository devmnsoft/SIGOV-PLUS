(function (window, $) {
  class SigovToast { show(message, variant) { var type = variant || 'info'; var item = $('<div class="toast align-items-center text-bg-' + type + ' border-0" role="status" aria-live="polite" aria-atomic="true"><div class="d-flex"><div class="toast-body"></div><button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Fechar"></button></div></div>'); item.find('.toast-body').text(message); $('#sigov-toast-container').append(item); if (window.bootstrap) { new window.bootstrap.Toast(item[0], { delay: 4500 }).show(); } else { item.show().delay(4500).fadeOut(); } } }
  window.Sigov = window.Sigov || {}; window.Sigov.toast = new SigovToast(); window.Sigov.SigovToast = SigovToast;
})(window, window.jQuery);
