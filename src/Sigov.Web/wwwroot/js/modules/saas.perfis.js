(function ($) {
  'use strict';
  $.getJSON('/api/saas/perfis/niveis').done(function (response) {
    var rows = (response.data || []).map(function (item) {
      return '<tr><td><code>' + item.codigo + '</code></td><td>' + item.nivelHierarquico + '</td><td>' + item.global + '</td><td>' + item.tenantAdmin + '</td></tr>';
    });
    $('#saasProfileLevels tbody').html(rows.join(''));
  });
})(jQuery);
