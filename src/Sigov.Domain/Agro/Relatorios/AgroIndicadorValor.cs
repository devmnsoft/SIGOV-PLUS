using Sigov.Domain.Common;

namespace Sigov.Domain.Agro.Relatorios;

public sealed class AgroIndicadorValor : Entity
{
    public AgroIndicadorValor(long tenantId, long indicadorId, decimal? valor, long? entidadeId = null, long? exercicioId = null, string? competencia = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (indicadorId <= 0) throw new ArgumentException("Indicador é obrigatório.", nameof(indicadorId));
        TenantId = tenantId; IndicadorId = indicadorId; Valor = valor ?? throw new ArgumentNullException(nameof(valor), "Valor de indicador não pode ser nulo."); EntidadeId = entidadeId; ExercicioId = exercicioId; Competencia = competencia;
    }
    public long TenantId { get; } public long? EntidadeId { get; } public long? ExercicioId { get; } public long IndicadorId { get; } public string? Competencia { get; } public decimal Valor { get; }
}
