using Sigov.Application.Commercial;

namespace Sigov.Application.Executive;

public sealed record ExecutiveIndicator(string Name, string Value, string Status);

public sealed record ExecutiveDashboardResponse(string EntityName, int FiscalYear, int ContractedModules, IReadOnlyList<ExecutiveIndicator> Indicators, IReadOnlyList<string> Alerts);

public interface IExecutiveDashboardService
{
    ExecutiveDashboardResponse GetDashboard();
}

public sealed class ExecutiveDashboardService : IExecutiveDashboardService
{
    private readonly IModuleCatalogService _moduleCatalogService;

    public ExecutiveDashboardService(IModuleCatalogService moduleCatalogService) => _moduleCatalogService = moduleCatalogService;

    public ExecutiveDashboardResponse GetDashboard()
    {
        var modules = _moduleCatalogService.GetModules();
        var indicators = new[]
        {
            new ExecutiveIndicator("Pessoas cadastradas", "0", "não disponível"),
            new ExecutiveIndicator("Usuários ativos", "0", "não disponível"),
            new ExecutiveIndicator("Processos abertos", "0", "não disponível"),
            new ExecutiveIndicator("Despesas pagas", "R$ 0,00", "não disponível"),
            new ExecutiveIndicator("Receitas arrecadadas", "R$ 0,00", "não disponível"),
            new ExecutiveIndicator("Contribuintes ativos", "0", "não disponível"),
            new ExecutiveIndicator("Servidores ativos", "0", "não disponível"),
            new ExecutiveIndicator("Alunos matriculados", "0", "não disponível"),
            new ExecutiveIndicator("Pacientes ativos", "0", "não disponível"),
            new ExecutiveIndicator("Chamados abertos", "0", "não disponível"),
            new ExecutiveIndicator("Integrações pendentes", "0", "operacional"),
            new ExecutiveIndicator("Alertas LGPD", "0", "operacional")
        };

        return new ExecutiveDashboardResponse("Entidade municipal atual", DateTime.UtcNow.Year, modules.Count(module => module.Status is ModuleStatus.Contratado or ModuleStatus.Habilitado or ModuleStatus.EmImplantacao), indicators, new[] { "Indicadores estruturais retornam zero quando tabelas do módulo ainda não existem." });
    }
}
