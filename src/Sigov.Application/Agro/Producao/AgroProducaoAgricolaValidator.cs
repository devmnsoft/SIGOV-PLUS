using Sigov.Domain.Agro;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Producao;

public sealed class AgroProducaoAgricolaValidator
{
    public Result Validar(AgroProducaoAgricolaCreateRequest r, long? exercicioId)
    { try { _ = new ProducaoAgricola(1, 1, exercicioId, r.ProdutorId, r.CulturaId, r.AreaPlantadaHa, r.ProducaoEstimada, r.ProducaoRealizada, r.DataPlantio, r.DataColheitaReal, r.UnidadeMedida, r.Status); return Result.Success(); } catch (ArgumentException ex) { return Result.Failure(ex.Message); } }
}
public sealed class AgroProducaoAgricolaMapper { public decimal? CalcularProdutividade(AgroProducaoAgricolaCreateRequest r) => Sigov.Domain.Agro.ProducaoAgricola.CalcularProdutividade(r.AreaPlantadaHa, r.ProducaoRealizada ?? r.ProducaoEstimada); }
