using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Agro.Permissions;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Propriedades;

public sealed class AgroPropriedadePermissionChecker { private readonly IAgroAccessChecker _access; public AgroPropriedadePermissionChecker(IAgroAccessChecker access) => _access = access; public Task<Result<AgroAccessContext>> CheckAsync(string p, CancellationToken ct) => _access.CheckAsync(new AgroAccessRequest(p, "agro.propriedades"), ct); }
public sealed class AgroPropriedadeAuditWriter { private readonly IAuditService _audit; private readonly ILogger<AgroPropriedadeAuditWriter> _logger; public AgroPropriedadeAuditWriter(IAuditService audit, ILogger<AgroPropriedadeAuditWriter> logger) { _audit = audit; _logger = logger; } public async Task WriteAsync(string tabela, string acao, string chave, object? novo, CancellationToken ct) { try { await _audit.RegistrarAsync("agro", acao, tabela, chave, null, novo, ct).ConfigureAwait(false); } catch (Exception ex) { _logger.LogWarning(ex, "Falha ao auditar Agro."); } } }
public sealed class AgroPropriedadeMapper { public string NormalizarJson(string? json) => string.IsNullOrWhiteSpace(json) ? "{}" : json; }
