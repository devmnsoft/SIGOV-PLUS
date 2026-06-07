namespace Sigov.Application.Onboarding;

public enum OnboardingStatus
{
    Pendente,
    EmAndamento,
    Concluido,
    Bloqueado
}

public sealed record OnboardingTaskDto(string Title, string Description, string TaskType, bool Required, OnboardingStatus Status, string Route);

public sealed record OnboardingStepDto(string Code, string Name, string Description, int Order, OnboardingStatus Status, decimal ProgressPercent, IReadOnlyList<OnboardingTaskDto> Tasks);

public sealed record OnboardingJourneyDto(long TenantId, string Name, OnboardingStatus Status, decimal ProgressPercent, IReadOnlyList<OnboardingStepDto> Steps);

public interface IOnboardingService
{
    OnboardingJourneyDto GetJourney(long tenantId);

    decimal CalculateProgress(IEnumerable<OnboardingStepDto> steps);
}

public sealed class OnboardingService : IOnboardingService
{
    public OnboardingJourneyDto GetJourney(long tenantId)
    {
        var steps = new[]
        {
            CreateStep("tenant", "Configurar tenant", "Revise domínio, ambiente e dados comerciais.", 1, "/SaasAdmin/Tenants"),
            CreateStep("entidade", "Configurar entidade", "Defina entidade principal, município e dados institucionais.", 2, "/Pessoas"),
            CreateStep("exercicio", "Configurar exercício", "Selecione exercício atual e parâmetros de operação.", 3, "/Financeiro/Dashboard"),
            CreateStep("usuarios", "Criar usuários", "Convide responsáveis e operadores por área.", 4, "/SaasAdmin/Operacao"),
            CreateStep("permissoes", "Definir perfis e permissões", "Aplique perfis mínimos e segregação de função.", 5, "/SaasAdmin/Operacao"),
            CreateStep("importacao", "Importar dados", "Planeje carga inicial e validações de qualidade.", 6, "/Integracoes/Remessas"),
            CreateStep("modulos", "Revisar módulos contratados", "Confirme catálogo contratado e rotas habilitadas.", 7, "/Modulos"),
            CreateStep("integracoes", "Configurar integrações", "Configure APIs, webhooks e remessas oficiais.", 8, "/Integracoes/Dashboard"),
            CreateStep("treinamento", "Treinar usuários", "Execute roteiro assistido por área.", 9, "/Ajuda"),
            CreateStep("lgpd", "Validar auditoria/LGPD", "Revise mascaramento, trilhas e acessos sensíveis.", 10, "/RegrasNegocio"),
            CreateStep("aceite", "Emitir termo de aceite", "Registre aceite operacional antes do go-live.", 11, "/Onboarding"),
            CreateStep("golive", "Go-live", "Finalize checklist e libere operação assistida.", 12, "/Executivo")
        };

        return new OnboardingJourneyDto(tenantId, "Implantação assistida sigov", OnboardingStatus.EmAndamento, CalculateProgress(steps), steps);
    }

    public decimal CalculateProgress(IEnumerable<OnboardingStepDto> steps)
    {
        var materialized = steps.ToArray();
        if (materialized.Length == 0)
        {
            return 0m;
        }

        return Math.Round(materialized.Average(step => step.ProgressPercent), 2, MidpointRounding.AwayFromZero);
    }

    private static OnboardingStepDto CreateStep(string code, string name, string description, int order, string route)
    {
        var task = new OnboardingTaskDto(name, description, "checklist", true, order <= 3 ? OnboardingStatus.Concluido : OnboardingStatus.Pendente, route);
        var progress = task.Status == OnboardingStatus.Concluido ? 100m : 0m;
        return new OnboardingStepDto(code, name, description, order, task.Status, progress, new[] { task });
    }
}
