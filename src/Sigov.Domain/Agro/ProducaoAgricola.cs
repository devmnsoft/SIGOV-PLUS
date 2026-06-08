using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class ProducaoAgricola : AggregateRoot
{
    public ProducaoAgricola(long tenantId, long entidadeId, long? exercicioId, long produtorId, long culturaId, decimal areaPlantadaHa, decimal? producaoEstimada, decimal? producaoRealizada, DateOnly? dataPlantio, DateOnly? dataColheitaReal, string unidadeMedida, string status)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId));
        if (produtorId <= 0) throw new ArgumentException("Produção exige produtor.", nameof(produtorId));
        if (culturaId <= 0) throw new ArgumentException("Produção exige cultura.", nameof(culturaId));
        if (areaPlantadaHa < 0) throw new ArgumentException("Área plantada não pode ser negativa.", nameof(areaPlantadaHa));
        if (producaoEstimada < 0) throw new ArgumentException("Produção estimada não pode ser negativa.", nameof(producaoEstimada));
        if (producaoRealizada < 0) throw new ArgumentException("Produção realizada não pode ser negativa.", nameof(producaoRealizada));
        if (dataPlantio.HasValue && dataColheitaReal.HasValue && dataColheitaReal < dataPlantio) throw new ArgumentException("Data de colheita real não pode ser menor que data de plantio.", nameof(dataColheitaReal));
        if (string.IsNullOrWhiteSpace(unidadeMedida)) throw new ArgumentException("Unidade de medida é obrigatória.", nameof(unidadeMedida));
        if (string.IsNullOrWhiteSpace(status)) throw new ArgumentException("Status da produção é obrigatório.", nameof(status));
        TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; ProdutorId = produtorId; CulturaId = culturaId; AreaPlantadaHa = areaPlantadaHa; ProducaoEstimada = producaoEstimada; ProducaoRealizada = producaoRealizada; DataPlantio = dataPlantio; DataColheitaReal = dataColheitaReal; UnidadeMedida = unidadeMedida.Trim(); Status = status.Trim(); Produtividade = CalcularProdutividade(areaPlantadaHa, producaoRealizada ?? producaoEstimada);
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public long ProdutorId { get; }
    public long CulturaId { get; }
    public decimal AreaPlantadaHa { get; }
    public decimal? ProducaoEstimada { get; }
    public decimal? ProducaoRealizada { get; }
    public DateOnly? DataPlantio { get; }
    public DateOnly? DataColheitaReal { get; }
    public string UnidadeMedida { get; }
    public string Status { get; }
    public decimal? Produtividade { get; }
    public static decimal? CalcularProdutividade(decimal areaPlantadaHa, decimal? producao) => areaPlantadaHa > 0 && producao.HasValue ? Math.Round(producao.Value / areaPlantadaHa, 4) : null;
}
