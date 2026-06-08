using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ServicoMaquinaRural : AggregateRoot
{
    public ServicoMaquinaRural(long tenantId, long entidadeId, long? exercicioId, long produtorId, string numero, AgroServicoMaquinaTipo tipoServico, AgroServicoMaquinaStatus status, DateOnly? dataExecucao = null, decimal? horasTrabalhadas = null, decimal? areaAtendidaHa = null, decimal? horimetroInicial = null, decimal? horimetroFinal = null, decimal? odometroInicial = null, decimal? odometroFinal = null, decimal? valorCobrado = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (produtorId <= 0) throw new ArgumentException("Serviço exige produtor.", nameof(produtorId));
        if (string.IsNullOrWhiteSpace(numero)) throw new ArgumentException("Número do serviço é obrigatório.", nameof(numero));
        if (status == AgroServicoMaquinaStatus.EXECUTADO && !dataExecucao.HasValue) throw new ArgumentException("Serviço executado exige data de execução.", nameof(dataExecucao));
        if (horasTrabalhadas < 0) throw new ArgumentException("Horas trabalhadas não podem ser negativas.", nameof(horasTrabalhadas));
        if (areaAtendidaHa < 0) throw new ArgumentException("Área atendida não pode ser negativa.", nameof(areaAtendidaHa));
        if (valorCobrado < 0) throw new ArgumentException("Valor cobrado não pode ser negativo.", nameof(valorCobrado));
        if (horimetroInicial.HasValue && horimetroFinal.HasValue && horimetroFinal < horimetroInicial) throw new ArgumentException("Horímetro final não pode ser menor que inicial.", nameof(horimetroFinal));
        if (odometroInicial.HasValue && odometroFinal.HasValue && odometroFinal < odometroInicial) throw new ArgumentException("Odômetro final não pode ser menor que inicial.", nameof(odometroFinal));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; ProdutorId = produtorId; Numero = numero.Trim(); TipoServico = tipoServico; Status = status; DataExecucao = dataExecucao;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public long ProdutorId { get; }
    public string Numero { get; }
    public AgroServicoMaquinaTipo TipoServico { get; }
    public AgroServicoMaquinaStatus Status { get; }
    public DateOnly? DataExecucao { get; }
    public void Executar(DateOnly dataExecucao) { if (Status == AgroServicoMaquinaStatus.CANCELADO) throw new InvalidOperationException("Serviço cancelado não pode ser executado."); }
    public void ValidarEdicao(bool permissaoAdministrativa) { if (Status == AgroServicoMaquinaStatus.EXECUTADO && !permissaoAdministrativa) throw new InvalidOperationException("Serviço executado não pode ser editado sem permissão administrativa."); }
}
