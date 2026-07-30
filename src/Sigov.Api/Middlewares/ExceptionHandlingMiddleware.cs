using System.Diagnostics;
using Sigov.Api.Contracts;
using Sigov.Domain.Comercial;

namespace Sigov.Api.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var correlationId = context.Items[CorrelationIdMiddleware.HeaderName]?.ToString() ?? Activity.Current?.Id ?? context.TraceIdentifier;
            var (status, message) = ex switch
            {
                CommercialRuleException => (StatusCodes.Status400BadRequest, ex.Message),
                CommercialConflictException => (StatusCodes.Status409Conflict, ex.Message),
                CommercialNotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                UnauthorizedAccessException => (StatusCodes.Status403Forbidden, "Acesso não autorizado para o tenant."),
                _ => (StatusCodes.Status500InternalServerError, "Não foi possível processar a solicitação. Informe o código de correlação ao suporte.")
            };
            if (status >= 500) _logger.LogError(ex, "Erro inesperado na requisição {CorrelationId}.", correlationId);
            else _logger.LogWarning("Regra comercial rejeitada. Status={Status}; CorrelationId={CorrelationId}; Motivo={Motivo}", status, correlationId, ex.Message);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(message, correlationId)).ConfigureAwait(false);
        }
    }
}
