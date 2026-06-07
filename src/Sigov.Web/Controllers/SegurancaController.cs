using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Lote1;

namespace Sigov.Web.Controllers;

public sealed class SegurancaController : Controller
{
    public IActionResult Usuarios() => View(new UsuarioFormViewModel());
    public IActionResult UsuarioDetalhe(long id = 0) => View(id);
    public IActionResult Perfis() => View(new PerfilFormViewModel());
    public IActionResult Permissoes() => View(new PermissaoMatrixViewModel { Modulo = "Administração", Acoes = new[] { "Visualizar", "Criar", "Editar", "Excluir", "Auditar" } });
    public IActionResult Grupos() => View(new GrupoFormViewModel());
    public IActionResult HistoricoLogin() => View();
}
