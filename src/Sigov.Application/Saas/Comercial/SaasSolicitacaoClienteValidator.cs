using Sigov.Domain.Common;
using Sigov.Domain.Saas.Comercial;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasSolicitacaoClienteValidator
{
    public Result Validate(SaasSolicitacaoClienteCreateRequest request)
    {
        var entity = new SaasSolicitacaoCliente(0, request.NomeOrganizacao, request.NomeResponsavel, request.EmailResponsavel, SaasSolicitacaoStatus.Recebida);
        var validation = entity.Validate();
        if (validation.IsFailure) return validation;
        return request.AceiteTermos ? Result.Success() : Result.Failure("Aceite de termos é obrigatório para auto cadastro.");
    }
}
