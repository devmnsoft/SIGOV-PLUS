/**
 * Sistema Transversal de Alertas, Toasts e Confirmações do SIGOV PLUS
 * Padrão compartilhado do produto.
 */
(function ($) {
    'use strict';

    // Auto-dismiss de banners de sucesso em 8 segundos
    $(function () {
        setTimeout(function () {
            $('.sigov-banner--success').fadeOut(400, function () {
                $(this).remove();
            });
        }, 8000);
    });

    // Gerenciador de Toasts no canto da tela (máx 3 simultâneos)
    function ensureToastStack() {
        var $stack = $('#sigov-toast-stack');
        if (!$stack.length) {
            $stack = $('<div id="sigov-toast-stack" class="sigov-toast-stack" aria-live="polite"></div>');
            $('body').append($stack);
        }
        return $stack;
    }

    function showToast(tipo, mensagem) {
        if (!mensagem) return;
        var $stack = ensureToastStack();
        var $toasts = $stack.children('.sigov-toast');
        if ($toasts.length >= 3) {
            $toasts.first().remove();
        }

        var classe = 'sigov-toast--info';
        if (tipo === 'success' || tipo === 'ok') classe = 'sigov-toast--success';
        else if (tipo === 'danger' || tipo === 'error' || tipo === 'erro') classe = 'sigov-toast--danger';
        else if (tipo === 'warning' || tipo === 'aviso') classe = 'sigov-toast--warning';

        var $toast = $(
            '<div class="sigov-toast ' + classe + '" role="' + (classe === 'sigov-toast--danger' ? 'alert' : 'status') + '">' +
            '<span>' + $('<div>').text(mensagem).html() + '</span>' +
            '<button type="button" class="sigov-toast__close" aria-label="Fechar">&times;</button>' +
            '</div>'
        );

        $toast.find('.sigov-toast__close').on('click', function () {
            $toast.fadeOut(200, function () { $toast.remove(); });
        });

        $stack.append($toast);

        // Auto remover toast em 5s (ou 8s para erro)
        var tempo = (classe === 'sigov-toast--danger') ? 8000 : 5000;
        setTimeout(function () {
            $toast.fadeOut(300, function () { $toast.remove(); });
        }, tempo);
    }

    function showBanner(tipo, mensagem) {
        if (!mensagem) return;
        var $container = $('#sigov-alerts');
        if (!$container.length) return;

        var classe = 'sigov-banner--info alert-info';
        var role = 'status';
        if (tipo === 'success' || tipo === 'ok') { classe = 'sigov-banner--success alert-success'; role = 'status'; }
        else if (tipo === 'danger' || tipo === 'error' || tipo === 'erro') { classe = 'sigov-banner--danger alert-danger'; role = 'alert'; }
        else if (tipo === 'warning' || tipo === 'aviso') { classe = 'sigov-banner--warning alert-warning'; role = 'status'; }

        var $banner = $(
            '<div class="sigov-banner ' + classe + ' alert alert-dismissible fade show d-flex align-items-center justify-content-between shadow-sm" role="' + role + '">' +
            '<span>' + $('<div>').text(mensagem).html() + '</span>' +
            '<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Fechar"></button>' +
            '</div>'
        );

        $container.prepend($banner);
        if (classe.indexOf('success') !== -1) {
            setTimeout(function () {
                $banner.fadeOut(400, function () { $banner.remove(); });
            }, 8000);
        }
    }

    window.SigovAlerts = {
        toast: showToast,
        banner: showBanner
    };

    // Interceptação transversal de ações com [data-confirm]
    $(document).on('click', 'button[data-confirm], a[data-confirm], input[type="submit"][data-confirm]', function (e) {
        var $elem = $(this);
        if ($elem.data('sigov-confirmed')) {
            $elem.removeData('sigov-confirmed');
            return true;
        }

        e.preventDefault();
        var mensagem = $elem.attr('data-confirm') || 'Confirme antes de continuar.';
        var $modal = $('#sigovConfirmModal');

        if ($modal.length && typeof bootstrap !== 'undefined') {
            $('#sigovConfirmMessage').text(mensagem);
            var bsModal = bootstrap.Modal.getOrCreateInstance($modal[0]);
            var $confirmBtn = $modal.find('[data-sigov-confirm-ok]');

            $confirmBtn.off('click').one('click', function () {
                bsModal.hide();
                $elem.data('sigov-confirmed', true);
                if ($elem.is('button[type="submit"], input[type="submit"]') && $elem.closest('form').length) {
                    $elem.closest('form').trigger('submit');
                } else if ($elem.is('a') && $elem.attr('href')) {
                    window.location.href = $elem.attr('href');
                } else {
                    $elem.trigger('click');
                }
            });

            bsModal.show();
        } else {
            if (window.confirm(mensagem)) {
                $elem.data('sigov-confirmed', true);
                if ($elem.is('button[type="submit"], input[type="submit"]') && $elem.closest('form').length) {
                    $elem.closest('form').trigger('submit');
                } else if ($elem.is('a') && $elem.attr('href')) {
                    window.location.href = $elem.attr('href');
                } else {
                    $elem.trigger('click');
                }
            }
        }
    });

    $(document).on('submit', 'form[data-confirm]', function (e) {
        var $form = $(this);
        if ($form.data('sigov-confirmed')) {
            $form.removeData('sigov-confirmed');
            return true;
        }

        e.preventDefault();
        var mensagem = $form.attr('data-confirm') || 'Confirme antes de continuar.';
        var $modal = $('#sigovConfirmModal');

        if ($modal.length && typeof bootstrap !== 'undefined') {
            $('#sigovConfirmMessage').text(mensagem);
            var bsModal = bootstrap.Modal.getOrCreateInstance($modal[0]);
            var $confirmBtn = $modal.find('[data-sigov-confirm-ok]');

            $confirmBtn.off('click').one('click', function () {
                bsModal.hide();
                $form.data('sigov-confirmed', true);
                $form.trigger('submit');
            });

            bsModal.show();
        } else {
            if (window.confirm(mensagem)) {
                $form.data('sigov-confirmed', true);
                $form.trigger('submit');
            }
        }
    });

})(jQuery);
