(function ($) {
  'use strict';
  function tenantId() { return $('#saasTenantId').val(); }
  $('#saasLoadTenantModules').on('click', function () {
    $.getJSON('/api/saas/tenants/' + tenantId() + '/modulos').done(function (response) {
      $('#saasTenantModules').text(JSON.stringify(response.data, null, 2));
    });
  });
  $('.js-saas-module-status').on('click', function () {
    var code = $(this).closest('tr').data('module');
    var status = $(this).data('status');
    $.post('/api/saas/tenants/' + tenantId() + '/modulos/' + code + '/' + status).done(function () {
      $('#saasLoadTenantModules').trigger('click');
    });
  });
})(jQuery);
