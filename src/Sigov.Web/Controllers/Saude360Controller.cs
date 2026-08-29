using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Saude;

namespace Sigov.Web.Controllers;

[Authorize]
[Route("Saude")]
public sealed class Saude360Controller : Controller
{
    [HttpGet("ACS")]
    public IActionResult Acs() => Operacao("ACS360", "Território, cadastros, visitas, produção e sincronização de campo.", "/api/saude/acs-campo/dashboard");

    [HttpGet("ACS/{pagina}")]
    public IActionResult AcsPagina(string pagina) => pagina.ToLowerInvariant() switch
    {
        "territorios" or "areas" or "microareas" or "agentes" => Operacao($"Território ACS — {pagina}", "Cobertura territorial e histórico de vínculos por unidade e equipe.", "/api/saude/acs/microareas"),
        "domicilios" => RedirectToAction("AcsDomicilios", "Saude"),
        "individuos" => RedirectToAction("AcsIndividuos", "Saude"),
        "visitas" => RedirectToAction("AcsVisitas", "Saude"),
        "atividadescoletivas" => Operacao("Atividades coletivas", "Produção coletiva por unidade, equipe e microárea.", "/api/saude/acs-campo/atividades-coletivas", "/api/saude/acs-campo/relatorios/exportar-csv"),
        "marcadores" => Operacao("Marcadores alimentares", "Acompanhamento nominal protegido e consolidado territorial.", "/api/saude/acs-campo/consumo-alimentar"),
        "ocorrencias" or "focosrisco" => Operacao("Ocorrências e focos de risco", "Tratamento de riscos de campo sem geração de protocolo artificial.", "/api/saude/acs-campo/ocorrencias", "/api/saude/acs-campo/relatorios/exportar-csv"),
        "sincronizacao" or "conflitos" => Operacao("Sincronização offline", "Lotes idempotentes, erros sanitizados e conflitos para resolução administrativa.", "/api/saude/acs-offline/lotes", "/api/saude/acs-offline/relatorios/exportar-csv"),
        "exportacaoesus" => Operacao("Staging e-SUS/SISAB", "Validação auditável; envio final permanece bloqueado sem layout oficial versionado.", "/api/saude/esus/lotes"),
        _ => NotFound()
    };

    [HttpGet("Vigilancias")]
    [HttpGet("Vigilancias/{pagina}")]
    public IActionResult Vigilancias(string? pagina) => Operacao($"Vigilâncias{(pagina is null ? string.Empty : $" — {pagina}")}", "Eventos epidemiológicos, sanitários, ambientais e do trabalhador, com alertas, prazos e ações.", "/api/saude/operacao/vigilancia");

    [HttpGet("Relatorios")]
    public IActionResult Relatorios() => Operacao("Relatórios Saúde360", "Exportações exigem permissão específica e recebem proteção contra CSV injection.", "/api/saude/acs-campo/relatorios/resumo", "/api/saude/acs-campo/relatorios/exportar-csv");

    private IActionResult Operacao(string titulo, string descricao, string api, string? exportacao = null) => View("~/Views/Saude/Saude360Operacao.cshtml", new Saude360OperacaoViewModel
    {
        Titulo = titulo, Descricao = descricao, Api = api, ExportacaoApi = exportacao, DadoSensivel = titulo.Contains("dom", StringComparison.OrdinalIgnoreCase) || titulo.Contains("indiv", StringComparison.OrdinalIgnoreCase)
    });
}
