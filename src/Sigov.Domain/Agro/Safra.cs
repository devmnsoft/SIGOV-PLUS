using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class Safra : AggregateRoot
{
    public Safra(long tenantId, long entidadeId, long? exercicioId, string codigo, string nome, int anoInicio, int anoFim, DateOnly? dataInicio, DateOnly? dataFim, string status)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (string.IsNullOrWhiteSpace(codigo)) throw new ArgumentException("Safra exige código.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(nome)) throw new ArgumentException("Safra exige nome.", nameof(nome));
        if (anoFim < anoInicio) throw new ArgumentException("Ano fim não pode ser menor que ano início.", nameof(anoFim));
        if (dataInicio.HasValue && dataFim.HasValue && dataFim < dataInicio) throw new ArgumentException("Data fim não pode ser menor que data início.", nameof(dataFim));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status da safra é obrigatório.", nameof(status));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; Codigo = codigo.Trim(); Nome = nome.Trim(); AnoInicio = anoInicio; AnoFim = anoFim; DataInicio = dataInicio; DataFim = dataFim; Status = status.Trim();
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public int AnoInicio { get; }
    public int AnoFim { get; }
    public DateOnly? DataInicio { get; }
    public DateOnly? DataFim { get; }
    public string Status { get; }
}
