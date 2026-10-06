using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.Modules;

namespace Sigov.Web.Helpers;

/// <summary>
/// Contrato 403 padronizado da RC-SAAS-AUT no superfície Web: grava o motivo/detalhe canônicos
/// em HttpContext.Items (legíveis na reexecução interna) e devolve um ObjectResult 403 explícito
/// que dispara UseStatusCodePagesWithReExecute("/Home/Error/{0}") renderizando a página amigável
/// com o motivo. O desafio de [Authorize] para usuário desautenticado segue o caminho padrão
/// 302 -> /Auth/Login; este helper trata apenas o caso autenticado sem permissão/escopo/contrato.
/// </summary>
public static class ForbiddenResponse
{
    public const string MotivoItemKey = "SaasForbidden.Motivo";
    public const string DetalheItemKey = "SaasForbidden.Detalhe";

    public static ObjectResult Registrar(this ControllerBase controller, SaasForbiddenMotivo motivo, string? detalhe)
    {
        var wire = SaasForbiddenMotivos.ToWire(motivo);
        controller.HttpContext.Items[MotivoItemKey] = wire;
        controller.HttpContext.Items[DetalheItemKey] = detalhe;
        return new ObjectResult(new { ok = false, motivo = wire, detalhe }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
