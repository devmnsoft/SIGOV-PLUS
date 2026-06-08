using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Relatorios;

public sealed class AgroRelatorioModelo : AggregateRoot
{
    public AgroRelatorioModelo(long tenantId, long? entidadeId, string codigo, string nome, AgroRelatorioTipo tipoRelatorio, AgroRelatorioFormato formatoPadrao = AgroRelatorioFormato.HTML, bool publicoNoTenant = false, bool contemDadosPessoais = false, bool contemDadosSensiveis = false)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Relatório exige código."); Nome = Required(nome, "Relatório exige nome."); TipoRelatorio = tipoRelatorio; FormatoPadrao = formatoPadrao; PublicoNoTenant = contemDadosPessoais ? false : publicoNoTenant; ContemDadosPessoais = contemDadosPessoais; ContemDadosSensiveis = contemDadosSensiveis;
    }
    public long TenantId { get; } public long? EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public AgroRelatorioTipo TipoRelatorio { get; } public AgroRelatorioFormato FormatoPadrao { get; } public bool PublicoNoTenant { get; } public bool ContemDadosPessoais { get; } public bool ContemDadosSensiveis { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
