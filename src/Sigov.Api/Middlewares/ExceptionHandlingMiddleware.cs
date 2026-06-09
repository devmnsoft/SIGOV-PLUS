using System.Diagnostics;
using Sigov.Api.Contracts;

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
            _logger.LogError(ex, "Erro inesperado na requisição {CorrelationId}.", correlationId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Não foi possível processar a solicitação. Informe o código de correlação ao suporte.", correlationId)).ConfigureAwait(false);
        }
    }
}
