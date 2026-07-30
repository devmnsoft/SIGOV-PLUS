(function (root, factory) {
  const api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  root.SigovEnterpriseRequest = api;
}(typeof globalThis !== 'undefined' ? globalThis : this, function () {
  'use strict';

  function resolveEnterpriseMethod(id) {
    return id ? 'PUT' : 'POST';
  }

  function buildEnterpriseUrl(baseUrl, id) {
    const normalized = String(baseUrl || '').replace(/\/$/, '');
    return id ? `${normalized}/${encodeURIComponent(id)}` : normalized;
  }

  function withoutTenant(payload) {
    const body = { ...(payload || {}) };
    delete body.tenantId;
    delete body.TenantId;
    return body;
  }

  function buildEnterpriseRequest(id, payload) {
    return {
      method: resolveEnterpriseMethod(id),
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(withoutTenant(payload))
    };
  }

  function buildEnterpriseDeleteRequest() {
    return { method: 'DELETE', headers: {} };
  }

  return { resolveEnterpriseMethod, buildEnterpriseUrl, buildEnterpriseRequest, buildEnterpriseDeleteRequest };
}));
