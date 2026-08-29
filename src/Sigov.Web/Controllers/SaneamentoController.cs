using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Sigov.Web.Models.Saneamento;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SaneamentoController : Controller
{
    public IActionResult Dashboard()
    {
        ViewData["Titulo"] = "Dashboard";
        ViewData["Modulo"] = "comercial";
        ViewData["Recurso"] = "dashboard";
        return View("~/Views/Saneamento/Avancado.cshtml");
    }
    [HttpGet("/Saneamento/Clientes")]
    [HttpGet("/Saneamento/Consumidores")]
    public IActionResult Consumidores() => Tela("Clientes e responsáveis", "comercial", "consumidores");
    public IActionResult ConsumidorDetalhe(long id) { ViewData["ConsumidorId"] = id; return View(); }
    [Route("/Saneamento/Ligacoes")]
    public IActionResult Ligacoes() => Tela("Ligações de água e esgoto", "comercial", "ligacoes");
    public IActionResult UnidadesConsumidoras() => View(new UnidadeConsumidoraFormViewModel());
    public IActionResult UnidadeConsumidoraDetalhe(long id) { ViewData["UnidadeConsumidoraId"] = id; return View(); }
    [HttpGet("/Saneamento/Hidrometros")]
    public IActionResult Hidrometros() => View(new HidrometroFormViewModel());
    [HttpGet("/Saneamento/Hidrometros/Novo")]
    public IActionResult HidrometroNovo() => View("Hidrometros", new HidrometroFormViewModel());
    [HttpGet("/Saneamento/Hidrometros/{id:long}")]
    public IActionResult HidrometroDetalhe(long id) { ViewData["HidrometroId"] = id; return View("Hidrometros", new HidrometroFormViewModel()); }
    [Route("/Saneamento/Leituras")]
    public IActionResult Leituras() => Tela("Rotas, competências e leituras", "faturamento", "leituras");
    [HttpGet("/Saneamento/Faturamento")]
    [HttpGet("/Saneamento/Faturas")]
    public IActionResult Faturas() => Tela("Faturamento", "faturamento", "faturas");
    public IActionResult FaturaDetalhe(long id) { ViewData["FaturaId"] = id; return View(); }
    [HttpGet("/Saneamento/Arrecadacao")]
    public IActionResult Arrecadacoes() => Tela("Arrecadação, inadimplência e parcelamentos", "faturamento", "arrecadacoes");
    public IActionResult Parcelamentos() => View(new ParcelamentoSaneamentoFormViewModel());
    [Route("/Saneamento/OrdensServico")]
    public IActionResult OrdensServico() => Tela("Ordens de serviço", "operacao", "ordens");
    public IActionResult OrdemServicoDetalhe(long id) { ViewData["OrdemServicoId"] = id; return View(); }
    public IActionResult EquipesCampo() => View(new EquipeCampoSaneamentoFormViewModel());
    public IActionResult Laboratorio() => View(new LaboratorioAmostraFormViewModel());
    public IActionResult Rede() => View(new RedeSaneamentoTrechoFormViewModel());


    [Route("/Saneamento")]
    public IActionResult Index(string? q = null)
    {
        return Dashboard();
    }

    [Route("/Saneamento/Gis")]
    public IActionResult Gis() => Tela("Georreferenciamento", "gis-qualidade", "gis");

    [Route("/Saneamento/Consumidores/Novo")] public IActionResult ConsumidorNovo() => View(new SaneamentoConsumidorFormViewModel());
    [Route("/Saneamento/Consumidores/{id:long}")] public IActionResult ConsumidorDetalheRota(long id) => ConsumidorDetalhe(id);
    [HttpGet("/Saneamento/Atendimento")]
    public IActionResult Atendimento() => Tela("Atendimento comercial", "comercial", "atendimentos");

    [Route("/Saneamento/Relatorios")] public IActionResult Relatorios() => Tela("Relatórios CSV", "comercial", "relatorios");

    // Endereços canônicos do SIGCOS. As telas compartilham o workspace real e cada
    // recurso consulta sua API persistida; os aliases preservam os links legados.
    [HttpGet("/Saneamento/Consumidores/Create"), HttpGet("/Saneamento/Consumidores/Edit")]
    public IActionResult ConsumidorForm() => View("Consumidores", new SaneamentoConsumidorFormViewModel());
    [HttpGet("/Saneamento/Consumidores/Details")] public IActionResult ConsumidorDetails(long id) => ConsumidorDetalhe(id);
    [HttpGet("/Saneamento/UnidadesConsumidoras")] public IActionResult Unidades() => UnidadesConsumidoras();
    [HttpGet("/Saneamento/Ligacoes/Create"), HttpGet("/Saneamento/Ligacoes/Edit"), HttpGet("/Saneamento/Ligacoes/Details"), HttpGet("/Saneamento/Ligacoes/Cortar"), HttpGet("/Saneamento/Ligacoes/Religar")]
    public IActionResult LigacaoFluxo() => Ligacoes();
    [HttpGet("/Saneamento/Hidrometros/Create"), HttpGet("/Saneamento/Hidrometros/Edit"), HttpGet("/Saneamento/Hidrometros/Details"), HttpGet("/Saneamento/Hidrometros/Substituir"), HttpGet("/Saneamento/Hidrometros/Afericoes")]
    public IActionResult HidrometroFluxo() => Hidrometros();
    [HttpGet("/Saneamento/Rotas")] public IActionResult Rotas() => Tela("Rotas e setores", "faturamento", "rotas-leitura");
    [HttpGet("/Saneamento/Leituras/Create"), HttpGet("/Saneamento/Leituras/Importar"), HttpGet("/Saneamento/Leituras/Criticas")]
    public IActionResult LeituraFluxo() => Leituras();
    [HttpGet("/Saneamento/RevisoesConsumo")] public IActionResult RevisoesConsumo() => Tela("Revisões de consumo", "faturamento", "revisoes");
    [HttpGet("/Saneamento/Tarifas")] public IActionResult Tarifas() => Tela("Tabelas tarifárias", "comercial", "tarifas");
    [HttpGet("/Saneamento/Faturas/Details"), HttpGet("/Saneamento/Faturas/SegundaVia"), HttpGet("/Saneamento/Faturas/Cancelar")]
    public IActionResult FaturaFluxo() => Faturas();
    [HttpGet("/Saneamento/Inadimplencia")] public IActionResult Inadimplencia() => Tela("Inadimplência", "faturamento", "inadimplencia");
    [HttpGet("/Saneamento/Cobranca")] public IActionResult Cobranca() => Tela("Cobrança", "faturamento", "cobrancas");
    [HttpGet("/Saneamento/Parcelamentos/Create"), HttpGet("/Saneamento/Parcelamentos/Details")] public IActionResult ParcelamentoFluxo() => Parcelamentos();
    [HttpGet("/Saneamento/OrdensServico/Create"), HttpGet("/Saneamento/OrdensServico/Edit"), HttpGet("/Saneamento/OrdensServico/Details"), HttpGet("/Saneamento/OrdensServico/Executar")]
    public IActionResult OrdemServicoFluxo() => OrdensServico();
    [HttpGet("/Saneamento/MateriaisCampo")] public IActionResult MateriaisCampo() => Tela("Materiais de campo", "operacao", "materiais");
    [HttpGet("/Saneamento/Redes"), HttpGet("/Saneamento/Trechos")] public IActionResult Redes() => Rede();
    [HttpGet("/Saneamento/UnidadesOperacionais")] public IActionResult UnidadesOperacionais() => Tela("Unidades operacionais", "gis-qualidade", "unidades-operacionais");
    [HttpGet("/Saneamento/Laboratorio/Amostras"), HttpGet("/Saneamento/Laboratorio/Parametros"), HttpGet("/Saneamento/Laboratorio/Resultados"), HttpGet("/Saneamento/Laboratorio/Conformidade")]
    public IActionResult LaboratorioFluxo() => Laboratorio();

    private IActionResult Tela(string titulo, string modulo, string recurso)
    {
        ViewData["Titulo"] = titulo;
        ViewData["Modulo"] = modulo;
        ViewData["Recurso"] = recurso;
        return View("~/Views/Saneamento/Avancado.cshtml");
    }
}
