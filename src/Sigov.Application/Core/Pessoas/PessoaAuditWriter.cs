namespace Sigov.Application.Core.Pessoas;

public sealed class PessoaAuditWriter
{
    public string BuildResourceKey(long pessoaId) => $"sigov.pessoa:{pessoaId}";
}
