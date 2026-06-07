(function (window) {
  class SigovErrorMapper { message(status) { var map = { 400: 'Revise os dados enviados.', 401: 'Sua sessão expirou. Entre novamente.', 403: 'Você não possui permissão para esta operação.', 404: 'Registro não encontrado.', 409: 'Conflito com regra de negócio.', 422: 'Existem inconsistências de validação.', 500: 'Erro interno. Tente novamente ou acione o suporte.' }; return map[status] || 'Não foi possível concluir a operação.'; } }
  class SigovProblemDetailsRenderer { render(problem) { return problem && (problem.detail || problem.title) ? (problem.detail || problem.title) : 'Erro não identificado.'; } }
  class SigovEmptyState { attach(selector, message) { var target = document.querySelector(selector); if (target && !target.children.length) { target.innerHTML = '<div class="sigov-empty-state p-4 text-center text-muted">' + message + '</div>'; } } }
  window.Sigov = window.Sigov || {}; window.Sigov.errorMapper = new SigovErrorMapper(); window.Sigov.SigovErrorMapper = SigovErrorMapper; window.Sigov.SigovProblemDetailsRenderer = SigovProblemDetailsRenderer; window.Sigov.SigovEmptyState = SigovEmptyState;
})(window);
