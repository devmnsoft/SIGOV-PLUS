using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.PostBuild;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ModuloController : Controller
{
    private readonly ILogger<ModuloController> _logger;

    private static readonly IReadOnlyDictionary<string, (string Nome, string Descricao, string[] Funcionalidades)> Catalogo =
        new Dictionary<string, (string, string, string[])>(StringComparer.OrdinalIgnoreCase)
        {
            ["tributario"] = ("Tributário", "Receitas municipais, arrecadação, dívida ativa e atendimento fiscal em uma experiência SaaS segura.", new[] { "Cadastro fiscal e contribuintes", "Lançamentos, DAM e parcelamentos", "Dívida ativa e certidões", "Painéis de arrecadação" }),
            ["protocolo"] = ("Protocolo", "Atendimento, processos digitais, ouvidoria e tramitação com SLA e auditoria.", new[] { "Abertura de protocolos", "Tramitação por setor", "Anexos e pareceres", "Consulta pública" }),
            ["ged"] = ("GED/OCR", "Gestão eletrônica de documentos com OCR, metadados, workflow e proteção LGPD.", new[] { "Upload e indexação", "OCR assistido", "Workflow documental", "Histórico auditado" }),
            ["contratos"] = ("Contratos", "Gestão contratual, assinaturas, vigências, medições e alertas gerenciais.", new[] { "Cadastro de contratos", "Vigências e aditivos", "Assinatura digital simulada", "Alertas de vencimento" }),
            ["juridico"] = ("Jurídico", "Processos jurídicos, pareceres, prazos e integração documental.", new[] { "Processos e partes", "Prazos e intimações", "Pareceres", "Relatórios gerenciais" }),
            ["mobile-campo"] = ("Mobile/Campo", "Operação de equipes externas com PWA, formulários, evidências e sincronização segura.", new[] { "Agenda de campo", "Formulários responsivos", "Coleta de evidências", "Modo offline" }),
            ["ia"] = ("IA", "Assistentes, automações e relatórios com revisão humana e governança LGPD.", new[] { "Assistente operacional", "Resumo documental", "Relatórios assistidos", "Alertas inteligentes" })
        };

    public ModuloController(ILogger<ModuloController> logger) => _logger = logger;

    [HttpGet("/Modulo/EmImplantacao")]
    [HttpGet("/OrdemServico/{*path}")]
    [HttpGet("/Industrial/{*path}")]
    [HttpGet("/Estoque/{*path}")]
    [HttpGet("/Varejo/{*path}")]
    [HttpGet("/Atacado/{*path}")]
    [HttpGet("/Mobile/{*path}")]
    [HttpGet("/Campo/{*path}")]
    public IActionResult EmImplantacao(string? codigo, string? path)
    {
        var key = !string.IsNullOrWhiteSpace(codigo) ? codigo : RouteData.Values["controller"]?.ToString() ?? path ?? "modulo";
        key = key.Trim('/').Replace('_', '-').ToLowerInvariant();
        var item = Catalogo.TryGetValue(key, out var catalogo) ? catalogo : CriarGenerico(key);

        _logger.LogInformation("Módulo em implantação acessado. Codigo={Codigo} Path={Path} CorrelationId={CorrelationId}", key, HttpContext.Request.Path, HttpContext.TraceIdentifier);

        return View("~/Views/Shared/ModuloEmPreparacao.cshtml", new ImplementationModuleViewModel
        {
            Codigo = key,
            Titulo = item.Nome,
            Descricao = item.Descricao,
            Status = "Em implantação",
            ProximosPassos = item.Funcionalidades,
            ModulosRelacionados = new[] { "Dashboard", "Catálogo de módulos", "Implantação guiada", "Auditoria/LGPD" }
        });
    }

    private static (string Nome, string Descricao, string[] Funcionalidades) CriarGenerico(string codigo)
    {
        var nome = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(codigo.Replace('-', ' '));
        return (nome, "Módulo previsto no roadmap comercial do SIGOV PLUS, preparado para implantação incremental sem gerar erro 404.", new[] { "Tela de entrada padronizada", "CRUD com auditoria", "Permissões por perfil", "Indicadores executivos" });
    }
}
