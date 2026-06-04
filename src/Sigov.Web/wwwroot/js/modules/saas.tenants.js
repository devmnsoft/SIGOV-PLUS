(async () => {
  const status = document.getElementById('sigov-tenant-status');
  if (!status || !window.sigovApi) return;
  try {
    const response = await window.sigovApi.request('/api/saas/tenant/atual', {
      headers: { 'X-Sigov-Tenant': 'municipio-demo' }
    });
    if (response && response.data) {
      status.textContent = `Tenant ${response.data.tenantSlug || response.data.TenantSlug} • Status ${response.data.status || response.data.Status}`;
    }
  } catch {
    status.textContent = 'Tenant não resolvido';
  }
})();
