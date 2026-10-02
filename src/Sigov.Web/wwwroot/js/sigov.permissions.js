(function (window, $) {
  window.Sigov = window.Sigov || {};
  window.Sigov.permissions = {
    hasPermission: function (perm) {
      if (!perm) return true;
      var perms = window.Sigov.userPermissions;
      if (Array.isArray(perms)) {
        return perms.indexOf(perm) !== -1 || perms.indexOf('*') !== -1;
      }
      return true;
    },
    apply: function () {
      // Nota de governança de segurança:
      // Elementos do DOM e scripts de cliente são orientados exclusivamente à usabilidade e experiência do usuário (UX).
      // A autorização e a consistência das operações são aplicadas de forma autoritativa no servidor por endpoint e por objeto.
      $('[data-permission]').each(function () {
        var $el = $(this);
        var perm = $el.attr('data-permission');
        $el.attr('data-sigov-permission-checked', 'true');
        if (window.Sigov.userPermissions && !window.Sigov.permissions.hasPermission(perm)) {
          $el.attr('hidden', 'hidden').prop('disabled', true).addClass('d-none');
        }
      });
    }
  };
  $(window.Sigov.permissions.apply);
})(window, window.jQuery);
