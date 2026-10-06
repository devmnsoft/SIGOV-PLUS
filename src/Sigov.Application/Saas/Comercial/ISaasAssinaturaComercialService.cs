namespace Sigov.Application.Saas.Comercial;

/// <summary>
/// RC-SAAS-AUT Etapa B - operações comerciais canônicas da família B (plano/assinatura/vigência/limites).
/// Cada operação executa em uma única transação, preserva dados (downgrade/suspensão/cancelamento
/// nunca apagam módulos contratados) e grava histórico + evento de auditoria.
/// </summary>
public interface ISaasAssinaturaComercialService
{
    Task<SaasComercialResultado> CriarAsync(long tenantId, long planoId, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasComercialResultado> UpgradeAsync(long tenantId, long novoPlanoId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasComercialResultado> DowngradeAsync(long tenantId, long novoPlanoId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasComercialResultado> SuspenderAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasComercialResultado> ReativarAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasComercialResultado> CancelarAsync(long tenantId, string? motivo, long usuarioId, string correlationId, CancellationToken cancellationToken = default);
}

public sealed record SaasComercialResultado(bool Sucesso, string Mensagem, IReadOnlyCollection<string>? ModulosSuspensos = null)
{
    public static SaasComercialResultado Ok(string mensagem, IReadOnlyCollection<string>? modulosSuspensos = null) => new(true, mensagem, modulosSuspensos);
    public static SaasComercialResultado Falha(string mensagem) => new(false, mensagem);
}

/// <summary>Difere os módulos incluídos entre dois planos (puro, testável).</summary>
public static class PlanModuleDiff
{
    public static (IReadOnlyList<string> Adicionados, IReadOnlyList<string> Removidos) Calcular(
        IReadOnlyCollection<string> antes, IReadOnlyCollection<string> depois)
    {
        var setAntes = new HashSet<string>(antes, StringComparer.OrdinalIgnoreCase);
        var setDepois = new HashSet<string>(depois, StringComparer.OrdinalIgnoreCase);
        return (
            setDepois.Where(m => !setAntes.Contains(m)).OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList(),
            setAntes.Where(m => !setDepois.Contains(m)).OrderBy(m => m, StringComparer.OrdinalIgnoreCase).ToList());
    }
}

/// <summary>Limite efetivo de usuários (puro): limite do plano + bônus de addons ATIVOS de usuários.</summary>
public static class EffectiveUserLimit
{
    /// <summary>Plano ilimitado (null) permanece ilimitado; caso contrário soma o bônus dos addons.</summary>
    public static int? SomarBonus(int? limiteBase, int bonusUsuarios)
    {
        if (limiteBase is null)
            return null;
        if (bonusUsuarios <= 0)
            return limiteBase;
        return limiteBase.Value + bonusUsuarios;
    }
}
