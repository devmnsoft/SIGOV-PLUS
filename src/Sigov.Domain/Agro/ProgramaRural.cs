using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ProgramaRural : AggregateRoot
{
    public ProgramaRural(long tenantId, long entidadeId, string codigo, string nome, AgroProgramaTipo tipoPrograma, DateOnly? vigenciaInicio = null, DateOnly? vigenciaFim = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (vigenciaInicio.HasValue && vigenciaFim.HasValue && vigenciaFim < vigenciaInicio) throw new ArgumentException("Vigência final não pode ser menor que vigência inicial.", nameof(vigenciaFim));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Programa exige código."); Nome = Required(nome, "Programa exige nome."); TipoPrograma = tipoPrograma; VigenciaInicio = vigenciaInicio; VigenciaFim = vigenciaFim;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public AgroProgramaTipo TipoPrograma { get; }
    public DateOnly? VigenciaInicio { get; }
    public DateOnly? VigenciaFim { get; }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
