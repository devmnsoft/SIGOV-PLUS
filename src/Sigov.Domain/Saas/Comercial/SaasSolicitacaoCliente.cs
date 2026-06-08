using System.Text.RegularExpressions;
using Sigov.Domain.Common;

namespace Sigov.Domain.Saas.Comercial;

public sealed class SaasSolicitacaoCliente : Entity
{
    private static readonly Regex EmailRegex = new("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    public SaasSolicitacaoCliente(long id, string nomeOrganizacao, string nomeResponsavel, string emailResponsavel, SaasSolicitacaoStatus status)
    {
        Id = id;
        NomeOrganizacao = nomeOrganizacao?.Trim() ?? string.Empty;
        NomeResponsavel = nomeResponsavel?.Trim() ?? string.Empty;
        EmailResponsavel = emailResponsavel?.Trim() ?? string.Empty;
        Status = status;
    }

    public string NomeOrganizacao { get; }
    public string NomeResponsavel { get; }
    public string EmailResponsavel { get; }
    public SaasSolicitacaoStatus Status { get; private set; }

    public Result Validate()
    {
        var errors = new List<ValidationError>();
        if (string.IsNullOrWhiteSpace(NomeOrganizacao)) errors.Add(new ValidationError(nameof(NomeOrganizacao), "Nome da organização é obrigatório."));
        if (string.IsNullOrWhiteSpace(NomeResponsavel)) errors.Add(new ValidationError(nameof(NomeResponsavel), "Responsável é obrigatório."));
        if (string.IsNullOrWhiteSpace(EmailResponsavel) || !EmailRegex.IsMatch(EmailResponsavel)) errors.Add(new ValidationError(nameof(EmailResponsavel), "E-mail válido é obrigatório."));
        return errors.Count == 0 ? Result.Success() : Result.ValidationFailure(errors);
    }

    public Result Converter()
    {
        if (Status == SaasSolicitacaoStatus.ConvertidaTenant) return Result.Failure("Solicitação convertida não pode converter novamente.");
        Status = SaasSolicitacaoStatus.ConvertidaTenant;
        return Result.Success();
    }
}
