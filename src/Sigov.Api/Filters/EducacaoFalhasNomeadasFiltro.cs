using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Api.Contracts;
using Sigov.Application.Educacao.Avancada;

namespace Sigov.Api.Filters;

/// <summary>
/// Ciclo 2 Sprint 2 (BR-2.6): diagnóstico uniforme para schema ausente em TODOS os caminhos dos
/// controllers avançados de educação (listar/criar/status/dashboard/csv): um único 404
/// RECURSO_INDISPONIVEL com a mensagem padrão da <see cref="RecursoNaoDisponivelException"/>.
/// Atributo herdado (aplica-se apenas aos controllers da família educacao.avancada) e atua somente
/// nas exceções que o endpoint não mapeou explicitamente (leituras sem try/catch).
/// </summary>
public sealed class EducacaoFalhasNomeadasFiltro : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is RecursoNaoDisponivelException ex)
        {
            context.ExceptionHandled = true;
            context.Result = new ObjectResult(ApiResponse<object>.Fail(ex.Message, context.HttpContext?.TraceIdentifier ?? string.Empty))
            {
                StatusCode = StatusCodes.Status404NotFound,
            };
        }
    }
}
