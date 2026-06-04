window.sigovApi = (() => {
  const baseUrl = window.Sigov_API_BASE_URL || 'http://localhost:5001';

  async function request(path, options = {}) {
    const correlationId = crypto.randomUUID();
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
      throw new Error(problem.detail || problem.title || 'Falha ao processar solicitação.');
    }

    return response.json();
  }

  return { request };
})();
