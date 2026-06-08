using Sigov.Domain.Agro;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Produtores;

public sealed class AgroProdutorValidator
{
    public Result ValidarCriacao(AgroProdutorCreateRequest request)
    {
        try { _ = new ProdutorRural(1, 1, request.PessoaId, request.CodigoProdutor ?? "AUTO", request.TipoProdutor, request.Situacao); return Result.Success(); }
        catch (ArgumentException ex) { return Result.Failure(ex.Message); }
    }
    public Result ValidarAtualizacao(AgroProdutorUpdateRequest request)
    {
        try { _ = new ProdutorRural(1, 1, 1, request.CodigoProdutor, request.TipoProdutor, request.Situacao); return Result.Success(); }
        catch (ArgumentException ex) { return Result.Failure(ex.Message); }
    }
}
