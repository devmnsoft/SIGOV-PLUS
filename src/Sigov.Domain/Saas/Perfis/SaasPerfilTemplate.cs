using Sigov.Domain.Common;
using Sigov.Domain.Saas;

namespace Sigov.Domain.Saas.Perfis;

public sealed class SaasPerfilTemplate : Entity
{
    private static readonly ISet<string> NiveisBase = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PerfilNivelCodigos.AdministradorTenant, PerfilNivelCodigos.AdministradorEntidade, PerfilNivelCodigos.Coordenador,
        PerfilNivelCodigos.Diretor, PerfilNivelCodigos.Servidor, PerfilNivelCodigos.Operador, PerfilNivelCodigos.Consulta,
        PerfilNivelCodigos.Auditor, PerfilNivelCodigos.Suporte
    };

    public SaasPerfilTemplate(long id, string codigo, string nome, string nivelBase)
    {
        Id = id;
        Codigo = codigo?.Trim().ToUpperInvariant() ?? string.Empty;
        Nome = nome?.Trim() ?? string.Empty;
        NivelBase = nivelBase?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    public string Codigo { get; }
    public string Nome { get; }
    public string NivelBase { get; }

    public Result Validate()
    {
        if (string.IsNullOrWhiteSpace(Codigo) || string.IsNullOrWhiteSpace(Nome)) return Result.Failure("Template exige código e nome.");
        if (string.IsNullOrWhiteSpace(NivelBase)) return Result.Failure("Perfil template exige nível-base.");
        if (string.Equals(NivelBase, PerfilNivelCodigos.AdministradorGeral, StringComparison.OrdinalIgnoreCase)) return Result.Failure("Perfil local não pode derivar de ADMINISTRADOR_GERAL.");
        return NiveisBase.Contains(NivelBase) ? Result.Success() : Result.Failure("Nível-base inválido para template SaaS.");
    }
}
