using Sigov.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Sigov.Application.Agro.Produtores;

public sealed class AgroProdutorAuditWriter
{
    private readonly IAuditService _audit; private readonly ILogger<AgroProdutorAuditWriter> _logger;
    public AgroProdutorAuditWriter(IAuditService audit, ILogger<AgroProdutorAuditWriter> logger) { _audit = audit; _logger = logger; }
    public async Task WriteAsync(string acao, string chave, object? anterior, object? novo, CancellationToken ct)
    { try { await _audit.RegistrarAsync("agro", acao, "sigov.agro_produtor", chave, anterior, novo, ct).ConfigureAwait(false); } catch (Exception ex) { _logger.LogWarning(ex, "Falha ao auditar produtor Agro."); } }
}
