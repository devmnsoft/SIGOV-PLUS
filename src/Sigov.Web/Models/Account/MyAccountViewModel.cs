namespace Sigov.Web.Models.Account;

public sealed record MyAccountViewModel(
    string Nome,
    string Login,
    string Email,
    string Organizacao,
    IReadOnlyCollection<string> Perfis,
    IReadOnlyCollection<string> Permissoes,
    bool IsAuthenticated);
