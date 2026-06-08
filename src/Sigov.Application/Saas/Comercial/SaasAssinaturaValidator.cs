using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public sealed class SaasAssinaturaValidator
{
    public Result ValidateUpdate(SaasAssinaturaUpdateRequest request)
    {
        if (request.UsuariosContratados <= 0) return Result.Failure("Usuários contratados deve ser maior que zero.");
        if (request.ValorContratado is < 0) return Result.Failure("Valor contratado não pode ser negativo.");
        return Result.Success();
    }
}
