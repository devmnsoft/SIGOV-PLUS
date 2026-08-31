using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Rh;

namespace Sigov.Web.Controllers;

public sealed class RhController : Controller
{
    [Route("/RH")]
    public IActionResult Index() => RedirectToAction(nameof(Dashboard));
    public IActionResult Importacoes() => View();
    public IActionResult Pendencias() => View();
    public IActionResult Dashboard() => View();
    public IActionResult Servidores() => View(new RhRegistroViewModel("servidores", "Servidores"));
    [Route("/Rh/Servidores/Novo")]
    [Route("/RH/Servidores/Create")]
    public IActionResult ServidorCriar() => View(new RhRegistroViewModel("servidores", "Novo Servidor"));
    [Route("/RH/Servidores/Edit/{id:long}")]
    public IActionResult ServidorEditar(long id) { ViewData["ServidorId"] = id; return View(new RhRegistroViewModel("servidores", "Editar Servidor")); }
    [Route("/RH/Servidores/Details/{id:long}")]
    public IActionResult ServidorDetalhe(long id) { ViewData["ServidorId"] = id; return View(new RhRegistroViewModel("servidores", "Detalhe do Servidor")); }
    [Route("/RH/Servidores/Vinculos")]
    public IActionResult ServidorVinculos() => View("RegistroFunc12", new RhRegistroViewModel("vinculos", "Vínculos do servidor"));
    [Route("/RH/Servidores/Documentos")]
    public IActionResult ServidorDocumentos() => View("RegistroFunc12", new RhRegistroViewModel("servidor-documentos", "Documentos do servidor"));
    [Route("/RH/Servidores/Historico")]
    public IActionResult ServidorHistorico() => View("RegistroFunc12", new RhRegistroViewModel("historico-funcional", "Histórico funcional"));
    public IActionResult Cargos() => View(new RhRegistroViewModel("cargos", "Cargos"));
    public IActionResult Funcoes() => View("RegistroFunc12", new RhRegistroViewModel("funcoes", "Funções"));
    public IActionResult Dependentes() => View("RegistroFunc12", new RhRegistroViewModel("dependentes", "Dependentes"));
    public IActionResult Frequencia() => View("RegistroFunc12", new RhRegistroViewModel("frequencias", "Frequência"));
    public IActionResult Lotacoes() => View(new RhRegistroViewModel("lotacoes", "Lotações"));
    public IActionResult Vinculos() => View(new RhRegistroViewModel("vinculos", "Vínculos"));
    public IActionResult Folhas() => View(new RhRegistroViewModel("folhas", "Folhas de Pagamento"));
    public IActionResult Folha() => RedirectToAction(nameof(Folhas));
    [Route("/Rh/Folha/Calcular")]
    public IActionResult Calcular() => View("OperacaoFolha", new RhRegistroViewModel("folhas", "Calcular folha"));
    [Route("/Rh/Folha/Homologar")]
    public IActionResult Homologar() => View("OperacaoFolha", new RhRegistroViewModel("folhas", "Homologar folha"));
    public IActionResult Holerites() => View("RegistroFunc12", new RhRegistroViewModel("holerites", "Holerites"));
    public IActionResult IntegracaoFinanceira() => View("RegistroFunc12", new RhRegistroViewModel("integracoes-financeiras", "Integração financeira"));
    public IActionResult Relatorios() => View("Relatorios");
    public IActionResult Auditoria() => View("RegistroFunc12", new RhRegistroViewModel("auditoria", "Auditoria de RH"));
    public IActionResult FolhaCriar() => View(new RhRegistroViewModel("folhas", "Nova Folha"));
    public IActionResult FolhaDetalhe(long id) { ViewData["FolhaId"] = id; return View(new RhRegistroViewModel("folhas", "Detalhe da Folha")); }
    public IActionResult FolhaEventos() => View(new RhRegistroViewModel("folha-eventos", "Eventos da Folha"));
    public IActionResult EventosFolha() => RedirectToAction(nameof(FolhaEventos));
    public IActionResult FolhaLancamentos() => View(new RhRegistroViewModel("folha-lancamentos", "Lançamentos da Folha"));
    public IActionResult LancamentosFolha() => RedirectToAction(nameof(FolhaLancamentos));
    [Route("/RH/Ponto")]
    [Route("/RH/Pontos")]
    public IActionResult Pontos() => View(new RhRegistroViewModel("pontos", "Ponto e Frequência"));
    public IActionResult Ferias() => View(new RhRegistroViewModel("ferias", "Férias"));
    public IActionResult Afastamentos() => View(new RhRegistroViewModel("afastamentos", "Afastamentos"));
    public IActionResult SaudeOcupacional() => View(new RhRegistroViewModel("saude-ocupacional", "Saúde Ocupacional"));
    public IActionResult Esocial() => View(new RhRegistroViewModel("esocial", "eSocial Estrutural"));
    [Route("/RH/eSocial/Eventos")]
    public IActionResult EsocialEventos() => View("RegistroFunc12", new RhRegistroViewModel("esocial", "Eventos eSocial"));
    [Route("/RH/eSocial/Pendencias")]
    public IActionResult EsocialPendencias() => View("RegistroFunc12", new RhRegistroViewModel("esocial", "Pendências eSocial"));
    public IActionResult Atos() => View("RegistroFunc12", new RhRegistroViewModel("atos", "Atos funcionais"));
    public IActionResult Licencas() => View("RegistroFunc12", new RhRegistroViewModel("licencas", "Licenças"));
    public IActionResult BancoHoras() => View("RegistroFunc12", new RhRegistroViewModel("banco-horas", "Banco de horas"));
    public IActionResult OcorrenciasFrequencia() => View("RegistroFunc12", new RhRegistroViewModel("ocorrencias-frequencia", "Ocorrências de frequência"));
    public IActionResult QuadroPessoal() => View("RegistroFunc12", new RhRegistroViewModel("quadro-pessoal", "Quadro de pessoal"));
    public IActionResult Pericias() => View("RegistroFunc12", new RhRegistroViewModel("pericias", "Perícias"));
    public IActionResult Previdencia() => View("RegistroFunc12", new RhRegistroViewModel("previdencia", "Previdência"));
    public IActionResult Consignacoes() => View("RegistroFunc12", new RhRegistroViewModel("consignacoes", "Consignações"));
    public IActionResult Portal() => View();
    public IActionResult PortalContracheques() => View();
    public IActionResult PortalFerias() => View();
    public IActionResult PortalAfastamentos() => View();
    public IActionResult PortalPonto() => View();
    public IActionResult PortalSolicitacoes() => View();
    public IActionResult PortalDadosCadastrais() => View("Portal");
    public IActionResult PontoDashboard() => View();
    public IActionResult PontoJornadas() => View();
    public IActionResult PontoEscalas() => View();
    public IActionResult PontoRegistros() => View();
    public IActionResult PontoJustificativas() => View();
    public IActionResult PontoApuracao() => View();
    public IActionResult PontoEspelho() => View();
    public IActionResult FeriasDashboard() => View();
    public IActionResult FeriasProgramacao() => View();
    public IActionResult FeriasDetalhe() => View();
    public IActionResult AfastamentoDetalhe() => View();
    public IActionResult FeriasAfastamentosRelatorios() => View();
    public IActionResult PortalAtualizacaoCadastral() => View();
    public IActionResult PortalMensagens() => View();
    public IActionResult PortalAdminSolicitacoes() => View();
}
