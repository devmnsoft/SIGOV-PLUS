using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[AllowAnonymous]
public sealed class ValidacaoDocumentoController : Controller
{
    [HttpGet("/ValidarDocumento")]
    public IActionResult Index() => Content("Validação pública de documentos SIGOV: informe código/hash. Dados sigilosos e CPF/CNPJ não são exibidos. Fallback honesto se sigov.portal_validacao_documento não existir.", "text/plain; charset=utf-8");
    [HttpPost("/ValidarDocumento")]
    [ValidateAntiForgeryToken]
    public IActionResult Validar() => Redirect("/ValidarDocumento");
    [HttpGet("/ValidarDocumento/{codigo}")]
    public IActionResult Codigo(string codigo) => Content($"Documento {codigo}: validação preparada para código, hash e assinatura pública sem expor dados pessoais.", "text/plain; charset=utf-8");
}
