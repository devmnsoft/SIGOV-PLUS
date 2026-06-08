using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class AgendaMaquinaRural : AggregateRoot
{
    public AgendaMaquinaRural(long tenantId, long entidadeId, long? exercicioId, long maquinaId, DateTimeOffset dataInicio, DateTimeOffset dataFim, AgroAgendaMaquinaStatus status)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (maquinaId <= 0) throw new ArgumentException("Agenda exige máquina.", nameof(maquinaId));
        if (dataFim <= dataInicio) throw new ArgumentException("Data fim deve ser maior que data início.", nameof(dataFim));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; MaquinaId = maquinaId; DataInicio = dataInicio; DataFim = dataFim; Status = status;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public long MaquinaId { get; }
    public DateTimeOffset DataInicio { get; }
    public DateTimeOffset DataFim { get; }
    public AgroAgendaMaquinaStatus Status { get; }
    public bool Sobrepoe(AgendaMaquinaRural outra) => outra.MaquinaId == MaquinaId && Status != AgroAgendaMaquinaStatus.CANCELADA && outra.Status != AgroAgendaMaquinaStatus.CANCELADA && DataInicio < outra.DataFim && DataFim > outra.DataInicio;
    public void Executar() { if (Status == AgroAgendaMaquinaStatus.CANCELADA) throw new InvalidOperationException("Agenda cancelada não executa."); }
}
