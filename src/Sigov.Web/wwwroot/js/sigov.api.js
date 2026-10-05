window.sigovApi = (() => {
  // Destino da API: configuração explícita (window.Sigov_API_BASE_URL) tem prioridade absoluta.
  // Sem configuração: HTTP dev usa http://localhost:5001; HTTPS usa a origem atual (evita mixed content).
  const configuredBaseUrl = window.Sigov_API_BASE_URL ? String(window.Sigov_API_BASE_URL).replace(/\/+$/, '') : '';
  const baseUrl = configuredBaseUrl || (window.location.protocol === 'https:' ? window.location.origin : 'http://localhost:5001');
  if (!configuredBaseUrl) console.warn('Sigov: Sigov_API_BASE_URL não configurada; usando destino padrão ' + baseUrl + '.');
  let cachedToken = null;
  let tokenPromise = null;

  async function getAuthToken() {
    if (cachedToken) return cachedToken;
    if (tokenPromise) return tokenPromise;
    tokenPromise = (async () => {
      try {
        const resp = await fetch(window.location.origin + '/Auth/ApiToken', {
          headers: { 'Accept': 'application/json' },
          credentials: 'same-origin'
        });
        if (!resp.ok) return null;
        const data = await resp.json().catch(() => ({}));
        if (data && data.token) {
          cachedToken = data.token;
          window.Sigov_API_EXERCICIO_ID = data.exercicioId ?? null;
          window.Sigov_API_ERRO_EXERCICIO = data.erro ?? null;
          return data.token;
        }
        if (data && data.erro) window.Sigov_API_ERRO_EXERCICIO = data.erro;
        return null;
      } catch {
        return null;
      } finally {
        tokenPromise = null;
      }
    })();
    return tokenPromise;
  }

  function clearToken() { cachedToken = null; }

  async function request(path, options = {}) {
    const correlationId = (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : String(Date.now());

    const doFetch = async () => {
      const headers = {
        'Content-Type': 'application/json',
        'X-Correlation-Id': correlationId,
        ...(options.headers || {})
      };
      const token = await getAuthToken();
      if (token) headers['Authorization'] = 'Bearer ' + token;
      return await fetch(`${baseUrl}${path}`, {
        ...options,
        headers,
        credentials: 'omit'
      });
    };

    // Retry único em 401 (seguro): o 401 é emitido na autenticação, antes da execução da ação,
    // portanto a mutação ainda não foi processada; POST com Idempotency-Key permanece idempotente.
    let response = await doFetch();
    if (response.status === 401) {
      clearToken();
      response = await doFetch();
    }

    if (!response.ok) {
      let problem = {};
      try {
        const text = await response.text();
        try { problem = text ? JSON.parse(text) : {}; } catch { problem = { title: text || 'Erro inesperado' }; }
      } catch { /* corpo inválido */ }
      const error = new Error(problem.detail || problem.title || problem.message || 'Falha ao processar solicitação.');
      error.status = response.status;
      error.correlationId = correlationId;
      throw error;
    }

    const contentType = response.headers.get('Content-Type') || '';
    if (contentType.includes('application/octet-stream') || contentType.includes('text/csv')) {
      return await response.arrayBuffer();
    }
    return response.json();
  }

  return { request, getAuthToken, clearToken };
})();
