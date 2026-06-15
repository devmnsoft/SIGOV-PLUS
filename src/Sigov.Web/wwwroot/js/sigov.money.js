(function () {
    'use strict';

    console.info('SIGOV money loaded safely v3');

    function onlyDigits(value) {
        return (value || '').toString().replace(/\D/g, '');
    }

    function formatMoneyInput(input) {
        if (!input) return;

        var digits = onlyDigits(input.value);

        if (!digits) {
            input.value = '';
            return;
        }

        var number = parseInt(digits, 10) / 100;

        input.value = number.toLocaleString('pt-BR', {
            style: 'currency',
            currency: 'BRL'
        });
    }

    function unformatMoney(value) {
        var digits = onlyDigits(value);

        if (!digits) return '0.00';

        var number = parseInt(digits, 10) / 100;

        return number.toFixed(2);
    }

    function bindMoneyInputs(root) {
        root = root || document;

        var inputs = root.querySelectorAll('[data-sigov-money], .sigov-money, .money');

        Array.prototype.forEach.call(inputs, function (input) {
            if (!input || input.dataset.sigovMoneyBound === 'true') return;

            input.dataset.sigovMoneyBound = 'true';

            input.addEventListener('input', function () {
                formatMoneyInput(input);
            });

            input.addEventListener('blur', function () {
                formatMoneyInput(input);
            });

            if (input.value) {
                formatMoneyInput(input);
            }
        });
    }

    function bindFormSubmit(root) {
        root = root || document;

        var forms = root.querySelectorAll('form');

        Array.prototype.forEach.call(forms, function (form) {
            if (!form || form.dataset.sigovMoneySubmitBound === 'true') return;

            form.dataset.sigovMoneySubmitBound = 'true';

            form.addEventListener('submit', function () {
                var inputs = form.querySelectorAll('[data-sigov-money], .sigov-money, .money');

                Array.prototype.forEach.call(inputs, function (input) {
                    if (!input) return;

                    var hiddenName = input.getAttribute('data-sigov-money-target');

                    if (!hiddenName) return;

                    var target = form.querySelector('[name="' + hiddenName + '"]');

                    if (target) {
                        target.value = unformatMoney(input.value);
                    }
                });
            });
        });
    }

    function bind(root) {
        bindMoneyInputs(root);
        bindFormSubmit(root);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            bind(document);
        });
    } else {
        bind(document);
    }

    window.SigovMoney = {
        bind: bind,
        format: formatMoneyInput,
        unformat: unformatMoney
    };
})();
