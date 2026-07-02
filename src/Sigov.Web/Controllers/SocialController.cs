using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Social;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
public sealed class SocialController : Controller
{
    private readonly SectorModuleService? _sector;
    public SocialController(SectorModuleService sector) => _sector = sector;
    [Route("/Social")] public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken) => _sector is null ? View(new SocialDashboardViewModel()) : View("~/Views/Sectors/Module.cshtml", await _sector.BuildAsync("Social", "Assistência Social", "Famílias, pessoas, atendimentos, benefícios, visitas e relatórios com LGPD reforçada.", new[]{"Famílias","Pessoas","Atendimentos","Benefícios"}, new[]{"/Social/Familias","/Social/Pessoas","/Social/Atendimentos","/Social/Beneficios","/Social/Visitas","/Social/Relatorios"}, true, q, cancellationToken));
    public IActionResult Dashboard()=>View(new SocialDashboardViewModel()); public IActionResult Unidades()=>View(new SocialUnidadeFormViewModel()); public IActionResult Familias()=>View(new SocialFamiliaFormViewModel());
    [Route("/Social/Familias/Nova")] public IActionResult FamiliaNova()=>View(new SocialFamiliaFormViewModel());
    [HttpPost("/Social/Familias/Nova")][ValidateAntiForgeryToken] public IActionResult FamiliaNovaPost(SocialFamiliaFormViewModel model){ TempData["Warning"]="Cadastro real de família depende da tabela sigov.social_familia; nenhum benefício foi simulado."; return RedirectToAction(nameof(Familias)); }
    [Route("/Social/Familias/{id:long}")] public IActionResult FamiliaDetalhe(long id)=>View(new SocialFamiliaFormViewModel()); public IActionResult Pessoas()=>View(new SocialPessoaFormViewModel()); public IActionResult Cadastros()=>View(new SocialFamiliaFormViewModel()); public IActionResult Programas()=>View(new SocialProgramaFormViewModel()); public IActionResult Beneficios()=>View(new SocialBeneficioFormViewModel()); public IActionResult Concessoes()=>View(new SocialBeneficioConcessaoFormViewModel()); public IActionResult Atendimentos()=>View(new SocialAtendimentoFormViewModel()); public IActionResult AtendimentoDetalhe(long id)=>View(new SocialAtendimentoFormViewModel()); public IActionResult Visitas()=>View(new SocialVisitaFormViewModel()); public IActionResult Pareceres()=>View(new SocialParecerFormViewModel()); public IActionResult Acompanhamentos()=>View(new SocialAcompanhamentoFormViewModel()); public IActionResult Vigilancia()=>View(new SocialVigilanciaFormViewModel()); public IActionResult Relatorios()=>View(new SocialDashboardViewModel());
}
