using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Social;
namespace Sigov.Web.Controllers;
public sealed class SocialController : Controller
{
    public IActionResult Dashboard()=>View(new SocialDashboardViewModel()); public IActionResult Unidades()=>View(new SocialUnidadeFormViewModel()); public IActionResult Familias()=>View(new SocialFamiliaFormViewModel()); public IActionResult FamiliaDetalhe(long id)=>View(new SocialFamiliaFormViewModel()); public IActionResult Pessoas()=>View(new SocialPessoaFormViewModel()); public IActionResult Cadastros()=>View(new SocialFamiliaFormViewModel()); public IActionResult Programas()=>View(new SocialProgramaFormViewModel()); public IActionResult Beneficios()=>View(new SocialBeneficioFormViewModel()); public IActionResult Concessoes()=>View(new SocialBeneficioConcessaoFormViewModel()); public IActionResult Atendimentos()=>View(new SocialAtendimentoFormViewModel()); public IActionResult AtendimentoDetalhe(long id)=>View(new SocialAtendimentoFormViewModel()); public IActionResult Visitas()=>View(new SocialVisitaFormViewModel()); public IActionResult Pareceres()=>View(new SocialParecerFormViewModel()); public IActionResult Acompanhamentos()=>View(new SocialAcompanhamentoFormViewModel()); public IActionResult Vigilancia()=>View(new SocialVigilanciaFormViewModel());
}
