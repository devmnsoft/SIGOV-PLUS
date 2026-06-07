using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/seguranca")]
public sealed class SegurancaController : ControllerBase
{
    [HttpGet("usuarios")]
    public ActionResult<ApiResponse<object>> Usuarios() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("perfis")]
    public ActionResult<ApiResponse<object>> Perfis() => ApiResponse<object>.Ok(new { items = Array.Empty<object>(), page = 1, pageSize = 20 });

    [HttpGet("permissoes")]
    public ActionResult<ApiResponse<object>> Permissoes() => ApiResponse<object>.Ok(new { modulo = "Segurança", acoes = new[] { "visualizar", "criar", "editar", "excluir", "auditar" } });
}
