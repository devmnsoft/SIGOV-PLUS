window.sigovApi = (() => {
  const baseUrl = window.Sigov_API_BASE_URL || 'http://localhost:5001';

  async function request(path, options = {}) {
    const correlationId = (window.crypto && crypto.randomUUID) ? crypto.randomUUID() : String(Date.now());
    const response = await fetch(`${baseUrl}${path}`, {
      ...options,
      headers: {
        'Content-Type': 'application/json',
        'X-Correlation-Id': correlationId,
        ...(options.headers || {})
      }
    });

    if (!response.ok) {
      const problem = await response.json().catch(() => ({ title: 'Erro inesperado' }));
      const error = new Error(problem.detail || problem.title || problem.message || 'Falha ao processar solicitação.');
      error.status = response.status;
      error.correlationId = correlationId;
      throw error;
    }

    return response.json();
  }

  return { request };
})();
