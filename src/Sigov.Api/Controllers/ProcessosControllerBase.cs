using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

public abstract class ProcessosControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value));
        if (result.Error == "403") return Forbid();
        if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error));
        if (result.Error?.Contains("não encontrado", StringComparison.OrdinalIgnoreCase) == true) return NotFound(ApiResponse<T>.Fail(result.Error));
        return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida."));
    }

    protected ActionResult<ApiResponse<object>> FromResult(Result result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<object>.Ok(new { ok = true }));
        if (result.Error == "403") return Forbid();
        if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<object>.Fail(result.Error));
        return BadRequest(ApiResponse<object>.Fail(result.Error ?? "Requisição inválida."));
    }
}
