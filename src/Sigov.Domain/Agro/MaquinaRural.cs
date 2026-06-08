using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class MaquinaRural : AggregateRoot
{
    public MaquinaRural(long tenantId, long entidadeId, string codigo, string nome, AgroMaquinaTipo tipoMaquina, AgroMaquinaSituacao situacao, decimal? horimetroAtual = null, decimal? odometroAtual = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (horimetroAtual < 0) throw new ArgumentException("Horímetro não pode ser negativo.", nameof(horimetroAtual));
        if (odometroAtual < 0) throw new ArgumentException("Odômetro não pode ser negativo.", nameof(odometroAtual));
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = Required(codigo, "Máquina exige código."); Nome = Required(nome, "Máquina exige nome."); TipoMaquina = tipoMaquina; Situacao = situacao; HorimetroAtual = horimetroAtual; OdometroAtual = odometroAtual;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public AgroMaquinaTipo TipoMaquina { get; }
    public AgroMaquinaSituacao Situacao { get; }
    public decimal? HorimetroAtual { get; }
    public decimal? OdometroAtual { get; }
    public void ValidarPodeAgendar() { if (Situacao != AgroMaquinaSituacao.ATIVA) throw new InvalidOperationException("Máquina inativa não pode ser agendada."); }
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
}
