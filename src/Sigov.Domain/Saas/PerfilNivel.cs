namespace Sigov.Domain.Saas;

public enum PerfilNivel
{
    AdministradorGeral,
    AdministradorTenant,
    AdministradorEntidade,
    Coordenador,
    Diretor,
    Servidor,
    Operador,
    Consulta,
    Auditor,
    Suporte
}

public static class PerfilNivelCodigos
{
    public const string AdministradorGeral = "ADMINISTRADOR_GERAL";
    public const string AdministradorTenant = "ADMINISTRADOR_TENANT";
    public const string AdministradorEntidade = "ADMINISTRADOR_ENTIDADE";
    public const string Coordenador = "COORDENADOR";
    public const string Diretor = "DIRETOR";
    public const string Servidor = "SERVIDOR";
    public const string Operador = "OPERADOR";
    public const string Consulta = "CONSULTA";
    public const string Auditor = "AUDITOR";
    public const string Suporte = "SUPORTE";

    public static readonly ISet<string> GlobalAdminAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        AdministradorGeral,
        "SIGOV_ADMIN",
        "SUPER_ADMIN"
    };
}
