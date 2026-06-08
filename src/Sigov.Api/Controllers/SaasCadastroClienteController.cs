using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Saas.Comercial;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class SaasCadastroClienteController : ControllerBase
{
    private readonly ISaasSolicitacaoClienteService _service;
    private readonly ICurrentUser _currentUser;
    public SaasCadastroClienteController(ISaasSolicitacaoClienteService service, ICurrentUser currentUser) { _service = service; _currentUser = currentUser; }

    [HttpPost("api/publico/cadastro-cliente")]
    public async Task<ActionResult<ApiResponse<SaasSolicitacaoClienteResponse>>> Criar([FromBody] SaasSolicitacaoClienteCreateRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.CriarAsync(request, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<SaasSolicitacaoClienteResponse>.Fail(result.Error ?? "Solicitação inválida.")) : Ok(ApiResponse<SaasSolicitacaoClienteResponse>.Ok(result.Value!, "Solicitação recebida."));
    }

    [HttpGet("api/saas/solicitacoes-clientes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<SaasSolicitacaoClienteResponse>>>> Listar([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) => Ok(ApiResponse<IReadOnlyCollection<SaasSolicitacaoClienteResponse>>.Ok(await _service.ListAdminAsync(page, pageSize, cancellationToken).ConfigureAwait(false)));

    [HttpGet("api/saas/solicitacoes-clientes/{id:long}")]
    public async Task<ActionResult<ApiResponse<SaasSolicitacaoClienteResponse>>> Obter(long id, CancellationToken cancellationToken)
    {
        var row = await _service.GetAdminAsync(id, cancellationToken).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<SaasSolicitacaoClienteResponse>.Fail("Solicitação não encontrada.")) : Ok(ApiResponse<SaasSolicitacaoClienteResponse>.Ok(row));
    }

    [HttpPost("api/saas/solicitacoes-clientes/{id:long}/aprovar")]
    public async Task<ActionResult<ApiResponse<string>>> Aprovar(long id, [FromBody] AprovarSolicitacaoClienteRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<string>.Ok((await _service.AprovarAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false)).IsSuccess ? "Aprovada" : "Falha"));

    [HttpPost("api/saas/solicitacoes-clientes/{id:long}/converter-tenant")]
    public async Task<ActionResult<ApiResponse<long>>> Converter(long id, [FromBody] ConverterSolicitacaoEmTenantRequest request, CancellationToken cancellationToken)
    {
        var result = await _service.ConverterEmTenantAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? BadRequest(ApiResponse<long>.Fail(result.Error ?? "Conversão inválida.")) : Ok(ApiResponse<long>.Ok(result.Value));
    }

    [HttpPost("api/saas/solicitacoes-clientes/{id:long}/recusar")]
    public async Task<ActionResult<ApiResponse<string>>> Recusar(long id, [FromBody] RecusarSolicitacaoClienteRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<string>.Ok((await _service.RecusarAsync(id, request, _currentUser.UsuarioId ?? 0, cancellationToken).ConfigureAwait(false)).IsSuccess ? "Recusada" : "Falha"));
}
