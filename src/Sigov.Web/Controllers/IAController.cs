using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class IAController : Controller
{
    public IActionResult Dashboard() => View("IaPage", new IaPageViewModel("Dashboard IA", "ia_assistente", "ia.dashboard.visualizar", "/api/ia/dashboard", "Indicadores de consumo, sugestões, alertas, automações, documentos e relatórios."));
    public IActionResult Assistente() => View("IaPage", new IaPageViewModel("Assistente IA", "ia_assistente", "ia.assistente.acessar", "/api/ia/executar", "Pergunte ao assistente interno por módulo com contexto opcional e histórico auditado."));
    public IActionResult Sugestoes() => View("IaPage", new IaPageViewModel("Sugestões inteligentes", "ia_assistente", "ia.sugestoes.visualizar", "/api/ia/sugestoes", "Aprove, aplique ou rejeite sugestões rastreáveis."));
    public IActionResult Execucoes() => View("IaPage", new IaPageViewModel("Execuções IA", "ia_assistente", "ia.execucoes.visualizar", "/api/ia/execucoes", "Histórico de prompts, respostas, consumo e correlationId."));
    public IActionResult Automacoes() => View("IaPage", new IaPageViewModel("Automações IA", "ia_automacoes", "ia.automacoes.visualizar", "/api/ia/automacoes", "Cadastre gatilhos, condições, ações e confirmação humana para rotinas críticas."));
    public IActionResult Alertas() => View("IaPage", new IaPageViewModel("Alertas inteligentes", "ia_automacoes", "ia.alertas.visualizar", "/api/ia/alertas", "Alertas por prioridade, origem e status de resolução."));
    public IActionResult Predicoes() => View("IaPage", new IaPageViewModel("Predições IA", "ia_predicoes", "ia.predicoes.visualizar", "/api/ia/predicoes", "Riscos iniciais por regras para inadimplência, ruptura, atraso e contratos."));
    public IActionResult Documental() => View("IaPage", new IaPageViewModel("IA Documental", "ia_documental", "ia.documental.resumir", "/api/ia/documentos/{documentoId}/resumir", "Resumo, classificação, extração e revisão documental."));
    public IActionResult Relatorios() => View("IaPage", new IaPageViewModel("Relatórios por IA", "ia_relatorios", "ia.relatorios.gerar", "/api/ia/relatorios/gerar", "Geração assistida de relatórios com exportação textual."));
    public IActionResult Configuracao() => View("IaPage", new IaPageViewModel("Configuração IA", "ia_assistente", "ia.configuracao.visualizar", "/api/ia/configuracao", "Habilite IA, limites, provider padrão, envio externo e mascaramento LGPD."));
    public IActionResult Consumo() => View("IaPage", new IaPageViewModel("Consumo IA", "ia_assistente", "ia.consumo.visualizar", "/api/ia/consumo", "Interações, tokens, custo estimado, limites e integração com billing SaaS."));
}

public sealed record IaPageViewModel(string Title, string Module, string Permission, string ApiRoute, string Description);
