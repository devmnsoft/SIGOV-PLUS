using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.Comercial;

public sealed class SaasAssinatura : Entity
{
    public SaasAssinatura(long id, long tenantId, long planoId, SaasAssinaturaStatus status, DateOnly dataInicio, int usuariosContratados, bool planoPermiteWhiteLabel, bool planoPermiteDominioCustomizado)
    {
        Id = id;
        TenantId = tenantId;
        PlanoId = planoId;
        Status = status;
        DataInicio = dataInicio;
        UsuariosContratados = usuariosContratados;
        PlanoPermiteWhiteLabel = planoPermiteWhiteLabel;
        PlanoPermiteDominioCustomizado = planoPermiteDominioCustomizado;
    }

    public long TenantId { get; }
    public long PlanoId { get; }
    public SaasAssinaturaStatus Status { get; private set; }
    public DateOnly DataInicio { get; }
    public int UsuariosContratados { get; }
    public bool PlanoPermiteWhiteLabel { get; }
    public bool PlanoPermiteDominioCustomizado { get; }

    public Result Validate()
    {
        var errors = new List<ValidationError>();
        if (TenantId <= 0) errors.Add(new ValidationError(nameof(TenantId), "Assinatura exige tenant."));
        if (PlanoId <= 0) errors.Add(new ValidationError(nameof(PlanoId), "Assinatura exige plano."));
        if (UsuariosContratados <= 0) errors.Add(new ValidationError(nameof(UsuariosContratados), "Usuários contratados deve ser maior que zero."));
        if (Status == SaasAssinaturaStatus.Ativa && DataInicio == default) errors.Add(new ValidationError(nameof(DataInicio), "Assinatura ativa exige data de início."));
        return errors.Count == 0 ? Result.Success() : Result.ValidationFailure(errors);
    }

    public Result EnsureWhiteLabelAllowed(bool hasAddon) => PlanoPermiteWhiteLabel || hasAddon ? Result.Success() : Result.Failure("White label não permitido pelo plano ou addon.");
    public Result EnsureCustomDomainAllowed(bool hasAddon) => PlanoPermiteDominioCustomizado || hasAddon ? Result.Success() : Result.Failure("Domínio customizado não permitido pelo plano ou addon.");
    public bool BloqueiaAcessoComum() => Status is SaasAssinaturaStatus.Suspensa or SaasAssinaturaStatus.Cancelada or SaasAssinaturaStatus.Expirada;
    public bool PermiteOnboarding() => Status is SaasAssinaturaStatus.EmImplantacao or SaasAssinaturaStatus.Teste or SaasAssinaturaStatus.Demo;
}
