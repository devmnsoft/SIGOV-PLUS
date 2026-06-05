using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Common;
using Sigov.Application.Core;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/pessoas")]
public sealed class PessoasController : ProcessosControllerBase
{
    private readonly IPessoaCadastroService _service;
    private readonly ILogger<PessoasController> _logger;

    public PessoasController(IPessoaCadastroService service, ILogger<PessoasController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<PessoaResumoResponse>>>> Listar([FromQuery] PessoaFiltro filtro, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.ListarAsync(filtro, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint GET /api/pessoas.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PagedResult<PessoaResumoResponse>>.Fail("Erro interno ao listar pessoas."));
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiResponse<PessoaDetalheResponse>>> Obter(long id, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.ObterAsync(id, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint GET /api/pessoas/{PessoaId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<PessoaDetalheResponse>.Fail("Erro interno ao obter pessoa."));
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] PessoaCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.CriarAsync(request, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint POST /api/pessoas.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<long>.Fail("Erro interno ao criar pessoa."));
        }
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] PessoaUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.AtualizarAsync(id, request, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint PUT /api/pessoas/{PessoaId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao atualizar pessoa."));
        }
    }

    [HttpDelete("{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.ExcluirAsync(id, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint DELETE /api/pessoas/{PessoaId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao excluir pessoa."));
        }
    }

    [HttpPost("{id:long}/enderecos")]
    public async Task<ActionResult<ApiResponse<long>>> AdicionarEndereco(long id, [FromBody] EnderecoCreateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.AdicionarEnderecoAsync(id, request, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint POST /api/pessoas/{PessoaId}/enderecos.", id);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<long>.Fail("Erro interno ao adicionar endereço."));
        }
    }

    [HttpPut("{id:long}/enderecos/{enderecoId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarEndereco(long id, long enderecoId, [FromBody] EnderecoUpdateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.AtualizarEnderecoAsync(id, enderecoId, request, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint PUT /api/pessoas/{PessoaId}/enderecos/{EnderecoId}.", id, enderecoId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao atualizar endereço."));
        }
    }

    [HttpDelete("{id:long}/enderecos/{enderecoId:long}")]
    public async Task<ActionResult<ApiResponse<object>>> ExcluirEndereco(long id, long enderecoId, CancellationToken cancellationToken)
    {
        try
        {
            return FromResult(await _service.ExcluirEnderecoAsync(id, enderecoId, cancellationToken).ConfigureAwait(false));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint DELETE /api/pessoas/{PessoaId}/enderecos/{EnderecoId}.", id, enderecoId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao excluir endereço."));
        }
    }

    [HttpGet("export.{formato}")]
    public async Task<IActionResult> Exportar(string formato, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.ExportarAsync(formato, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error ?? "Falha na exportação."));
            var contentType = formato.ToLowerInvariant() switch { "json" => "application/json", "xml" => "application/xml", _ => "text/csv" };
            return File(result.Value ?? Array.Empty<byte>(), contentType, $"pessoas.{formato}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha inesperada no endpoint GET /api/pessoas/export.{Formato}.", formato);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.Fail("Erro interno ao exportar pessoas."));
        }
    }
}
