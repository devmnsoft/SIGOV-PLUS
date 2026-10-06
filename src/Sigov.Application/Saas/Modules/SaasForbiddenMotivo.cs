namespace Sigov.Application.Saas.Modules;

/// <summary>
/// Causa canônica de negação de acesso a módulo/feature (RC-SAAS-AUT).
/// O valor de wire (maiúsculo) integra o contrato da API (campo "motivo" do 403).
/// </summary>
public enum SaasForbiddenMotivo
{
    SemPermissao,
    ForaEscopo,
    ModuloNaoContratado,
    LimiteAtingido,
    BloqueadoComercial,
    ContratoExpirado
}

public static class SaasForbiddenMotivos
{
    public static string ToWire(SaasForbiddenMotivo motivo) => motivo switch
    {
        SaasForbiddenMotivo.SemPermissao => "SEM_PERMISSAO",
        SaasForbiddenMotivo.ForaEscopo => "FORA_ESCOPO",
        SaasForbiddenMotivo.ModuloNaoContratado => "MODULO_NAO_CONTRATADO",
        SaasForbiddenMotivo.LimiteAtingido => "LIMITE_ATINGIDO",
        SaasForbiddenMotivo.BloqueadoComercial => "BLOQUEADO_COMERCIAL",
        SaasForbiddenMotivo.ContratoExpirado => "CONTRATO_EXPIRADO",
        _ => "SEM_PERMISSAO"
    };
}

/// <summary>Classifica status do cliente (sigov.tenant.status) que bloqueia a atividade comercial.</summary>
public static class TenantCommercialStatus
{
    private static readonly HashSet<string> Blocked = new(StringComparer.OrdinalIgnoreCase)
    {
        "BLOQUEADO", "SUSPENSO", "INADIMPLENTE", "CANCELADO", "EXCLUIDO"
    };

    public static bool IsBlocked(string? status) => !string.IsNullOrWhiteSpace(status) && Blocked.Contains(status);
}
