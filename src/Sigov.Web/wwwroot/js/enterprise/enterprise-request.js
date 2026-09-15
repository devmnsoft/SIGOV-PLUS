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

  async function readApiResponse(response) {
    const contentType = response.headers.get('content-type') || '';
    if (response.redirected || !contentType.toLowerCase().includes('application/json')) {
      throw new Error(response.redirected || contentType.toLowerCase().includes('text/html')
        ? 'Sessão expirada ou resposta de autenticação inválida.'
        : 'Resposta inesperada do servidor.');
    }

    const payload = await response.json();
    if (!response.ok || payload.success === false || payload.Success === false) {
      throw new Error(payload.message || payload.Message || `HTTP ${response.status}`);
    }
    return payload;
  }

  function responseData(payload) {
    return payload?.data ?? payload?.Data;
  }

  function mutationId(payload, fallbackId) {
    const data = responseData(payload);
    return data?.id ?? data?.Id ?? fallbackId;
  }

  return { resolveEnterpriseMethod, buildEnterpriseUrl, buildEnterpriseRequest, buildEnterpriseDeleteRequest, readApiResponse, responseData, mutationId };
}));
