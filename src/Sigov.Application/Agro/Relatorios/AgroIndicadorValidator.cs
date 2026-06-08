using Sigov.Domain.Agro.Relatorios;

namespace Sigov.Application.Agro.Relatorios;

public sealed class AgroIndicadorValidator
{
    public AgroIndicador Validate(AgroIndicadorCreateRequest request, long tenantId, long? entidadeId)
    {
        var categoria = Enum.TryParse<AgroIndicadorCategoria>(request.Categoria, true, out var parsed) ? parsed : AgroIndicadorCategoria.PRODUTORES;
        return new AgroIndicador(tenantId, entidadeId, request.Codigo, request.Nome, categoria, request.Publico, request.ContemDadoPessoal);
    }
}
