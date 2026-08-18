using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/seguranca")]
public sealed class SegurancaController : ControllerBase
{
    [HttpGet("usuarios")]
    public ActionResult<ApiResponse<object>> Usuarios() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("perfis")]
    public ActionResult<ApiResponse<object>> Perfis() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("permissoes")]
    public ActionResult<ApiResponse<object>> Permissoes() => ApiResponse<object>.Ok(new { modulo = "Segurança", acoes = new[] { "visualizar", "criar", "editar", "excluir", "auditar" } });

    [HttpGet("permissoes/dashboard")]
    public ActionResult<ApiResponse<object>> Dashboard() => ApiResponse<object>.Ok(new { modelo = "modulo/recurso/acao", escopos = new[] { "GLOBAL", "TENANT", "ENTIDADE" }, exportacaoExigePermissao = true });

    [HttpGet("recursos")]
    public ActionResult<ApiResponse<object>> Recursos() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), estrutura = "seguranca_recurso" });

    [HttpGet("perfis/{id:long}/permissoes")]
    public ActionResult<ApiResponse<object>> PermissoesPerfil(long id) => ApiResponse<object>.Ok(new { perfilId = id, items = Array.Empty<object>() });

    [HttpPost("perfis/{id:long}/permissoes")]
    public IActionResult ConcederPerfil(long id, [FromBody] PermissaoRequest request) => StatusCode(StatusCodes.Status501NotImplemented, new { perfilId = id, request.PermissaoIds, mensagem = "Persistência granular pendente; nenhuma concessão foi simulada." });

    [HttpPost("perfis/{id:long}/permissoes/remover")]
    public IActionResult RemoverPerfil(long id, [FromBody] PermissaoRequest request) => StatusCode(StatusCodes.Status501NotImplemented, new { perfilId = id, request.PermissaoIds, mensagem = "Persistência granular pendente; nenhuma remoção foi simulada." });

    [HttpGet("usuarios/{id:long}/permissoes")]
    public ActionResult<ApiResponse<object>> PermissoesUsuario(long id) => ApiResponse<object>.Ok(new { usuarioId = id, items = Array.Empty<object>() });

    [HttpPost("usuarios/{id:long}/permissoes")]
    public IActionResult ConcederUsuario(long id, [FromBody] PermissaoRequest request) => StatusCode(StatusCodes.Status501NotImplemented, new { usuarioId = id, request.PermissaoIds, mensagem = "Persistência granular pendente; nenhuma concessão foi simulada." });

    [HttpPost("validar-permissao")]
    public ActionResult<ApiResponse<object>> Validar([FromBody] ValidarPermissaoRequest request) => ApiResponse<object>.Ok(new { request.Modulo, request.Recurso, request.Acao, permitido = false, motivo = "A avaliação efetiva exige usuário e tenant autenticados." });
}

public sealed record PermissaoRequest(long[] PermissaoIds);
public sealed record ValidarPermissaoRequest(string Modulo, string Recurso, string Acao, long? EntidadeId);
