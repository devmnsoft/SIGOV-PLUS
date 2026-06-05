using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Core;

namespace Sigov.Web.Controllers;

public sealed class PessoasController : Controller
{
    public IActionResult Index() => View(new PessoaFormViewModel());
    public IActionResult Criar() => View(new PessoaFormViewModel());
    public IActionResult Detalhe(long id) => View(id);
    public IActionResult Editar(long id) => View(new PessoaFormViewModel { Id = id });
}
