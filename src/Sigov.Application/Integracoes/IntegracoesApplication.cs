using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Domain.Common;
using Sigov.Domain.Integracoes;

namespace Sigov.Application.Integracoes;

public static class IntegracaoPermissoes
{
    public const string Modulo = "integracao";
    public const string ApiCredentialVisualizar = "integracao.api_credential.visualizar";
    public const string ApiCredentialCriar = "integracao.api_credential.criar";
    public const string ApiCredentialRevogar = "integracao.api_credential.revogar";
    public const string SistemaVisualizar = "integracao.sistema.visualizar";
    public const string SistemaCriar = "integracao.sistema.criar";
    public const string SistemaEditar = "integracao.sistema.editar";
    public const string SistemaExcluir = "integracao.sistema.excluir";
    public const string SistemaTestar = "integracao.sistema.testar";
    public const string WebhookVisualizar = "integracao.webhook.visualizar";
    public const string WebhookReceber = "integracao.webhook.receber";
    public const string WebhookEnviar = "integracao.webhook.enviar";
    public const string WebhookReprocessar = "integracao.webhook.reprocessar";
    public const string OutboxVisualizar = "integracao.outbox.visualizar";
    public const string OutboxCriar = "integracao.outbox.criar";
    public const string OutboxReprocessar = "integracao.outbox.reprocessar";
    public const string OutboxDeadLetter = "integracao.outbox.dead_letter";
    public const string RemessaVisualizar = "integracao.remessa.visualizar";
    public const string RemessaCriar = "integracao.remessa.criar";
    public const string RemessaGerar = "integracao.remessa.gerar";
    public const string RemessaEnviar = "integracao.remessa.enviar";
    public const string RemessaCancelar = "integracao.remessa.cancelar";
    public const string CertificadoVisualizar = "integracao.certificado.visualizar";
    public const string CertificadoCriar = "integracao.certificado.criar";
    public const string CertificadoRevogar = "integracao.certificado.revogar";
    public const string GovBrConfigurar = "integracao.govbr.configurar";
    public const string AssinadorUsar = "integracao.assinador.usar";
    public const string DashboardVisualizar = "integracao.dashboard.visualizar";
    public const string Exportar = "integracao.exportar";
}
public sealed record ApiCredentialCreateRequest(string Nome, string? Descricao, string ClientId, IReadOnlyCollection<string>? Scopes, IReadOnlyCollection<string>? PermitidoIps, DateTimeOffset? ExpiraAt);
public sealed record ApiCredentialCreateResponse(long Id, string ClientId, string ApiKey, string ApiKeyPrefix, DateTimeOffset? ExpiraAt);
public sealed record ApiCredentialResponse(long Id, string Nome, string ClientId, string ApiKeyPrefix, IReadOnlyCollection<string> Scopes, string Status, DateTimeOffset? ExpiraAt, DateTimeOffset? LastUsedAt);
public sealed record ApiCredentialFiltro(int Page = 1, int PageSize = 20, string? Status = null);
public sealed record RevogarApiCredentialRequest(string? Motivo);
public sealed record IntegracaoSistemaCreateRequest(string Codigo, string Nome, string TipoIntegracao, string Ambiente, string? BaseUrl, object? Configuracao);
public sealed record IntegracaoSistemaUpdateRequest(string Nome, string TipoIntegracao, string Ambiente, string? BaseUrl, string Status, object? Configuracao, bool Ativo);
public sealed record IntegracaoSistemaResponse(long Id, string Codigo, string Nome, string TipoIntegracao, string Ambiente, string? BaseUrl, string Status, bool Ativo);
public sealed record IntegracaoSistemaFiltro(int Page = 1, int PageSize = 20, string? TipoIntegracao = null, string? Status = null);
public sealed record WebhookRecebidoResponse(long Id, string Origem, string Evento, string Status, bool? AssinaturaValida, string? IdempotencyKey, DateTimeOffset CreatedAt);
public sealed record WebhookRecebidoFiltro(int Page = 1, int PageSize = 20, string? Status = null);
public sealed record WebhookReceberRequest(string Evento, object? Payload, string? IdempotencyKey);
public sealed record WebhookEnviadoResponse(long Id, string Destino, string Url, string Evento, string Status, int Tentativas, DateTimeOffset CreatedAt);
public sealed record WebhookEnviarRequest(string Destino, string Url, string Evento, object? Payload);
public sealed record IdempotencyCheckRequest(string Chave, string Metodo, string Rota, string RequestHash, TimeSpan Ttl);
public sealed record IdempotencyCheckResponse(bool PodeProcessar, string Status, string? Mensagem);
public sealed record OutboxEventoCreateRequest(string TipoEvento, object? Payload, object? Headers, string? Origem, string? Destino, string? IdempotencyKey, Guid? CorrelationId);
public sealed record OutboxEventoResponse(long Id, long? TenantId, string TipoEvento, string Status, int Tentativas, int MaxTentativas, bool DeadLetter, DateTimeOffset CreatedAt, Guid? CorrelationId);
public sealed record OutboxFiltro(int Page = 1, int PageSize = 20, string? Status = null, bool? DeadLetter = null);
public sealed record ReprocessarOutboxRequest(string? Motivo);
public sealed record MoverDeadLetterRequest(string? Motivo);
public sealed record RemessaOficialCreateRequest(string TipoRemessa, string? Competencia, string Numero, object? Payload);
public sealed record RemessaOficialResponse(long Id, string TipoRemessa, string? Competencia, string Numero, string Status, DateTimeOffset? GeradoAt, DateTimeOffset? EnviadoAt);
public sealed record GerarRemessaRequest(bool DevAdapter);
public sealed record EnviarRemessaDevRequest(bool DevAdapter);
public sealed record CertificadoDigitalCreateRequest(string Nome, string TipoCertificado, string? Subject, string? Issuer, string? SerialNumber, DateOnly? ValidadeInicio, DateOnly? ValidadeFim, string? StorageKey, string? Thumbprint, object? Metadados);
public sealed record CertificadoDigitalResponse(long Id, string Nome, string TipoCertificado, DateOnly? ValidadeFim, string Status, bool Ativo);
public sealed record IntegracaoDashboardResponse(long TotalSistemas, long SistemasAtivos, long ApiCredentialsAtivas, long OutboxPendentes, long OutboxErro, long OutboxDeadLetter, long WebhooksRecebidosHoje, long WebhooksEnviadosHoje, long WebhooksFalha, long RemessasPendentes, long RemessasEnviadasMes, long CertificadosAtivos, long CertificadosVencendo, IReadOnlyCollection<OutboxEventoResponse> UltimosEventosOutbox, IReadOnlyCollection<WebhookRecebidoResponse> UltimosWebhooksRecebidos, IReadOnlyCollection<RemessaOficialResponse> UltimasRemessas, IReadOnlyCollection<string> ErrosRecentes, IReadOnlyCollection<string> Alertas);

public interface IApiCredentialRepository { Task<PagedResult<ApiCredentialResponse>> ListarAsync(long tenantId, ApiCredentialFiltro filtro, CancellationToken ct); Task<ApiCredentialResponse?> ObterAsync(long tenantId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, ApiCredentialCreateRequest request, string prefix, string hash, string algoritmo, long? usuarioId, CancellationToken ct); Task AlterarStatusAsync(long tenantId, long id, string status, long? usuarioId, CancellationToken ct); }
public interface IApiCredentialService { Task<Result<PagedResult<ApiCredentialResponse>>> ListarAsync(ApiCredentialFiltro filtro, CancellationToken ct); Task<Result<ApiCredentialResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<ApiCredentialCreateResponse>> CriarAsync(ApiCredentialCreateRequest request, CancellationToken ct); Task<Result> RevogarAsync(long id, RevogarApiCredentialRequest request, CancellationToken ct); Task<Result> SuspenderAsync(long id, CancellationToken ct); Task<Result> ReativarAsync(long id, CancellationToken ct); }
public interface IApiKeyHasher { string GenerateApiKey(bool production, out string prefix); string Hash(string apiKey); bool Verify(string apiKey, string storedHash); string Algorithm { get; } }
public interface IIntegracaoSistemaRepository { Task<PagedResult<IntegracaoSistemaResponse>> ListarAsync(long tenantId, IntegracaoSistemaFiltro filtro, CancellationToken ct); Task<IntegracaoSistemaResponse?> ObterAsync(long tenantId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, IntegracaoSistemaCreateRequest request, long? usuarioId, CancellationToken ct); Task AtualizarAsync(long tenantId, long id, IntegracaoSistemaUpdateRequest request, long? usuarioId, CancellationToken ct); Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken ct); }
public interface IIntegracaoSistemaService { Task<Result<PagedResult<IntegracaoSistemaResponse>>> ListarAsync(IntegracaoSistemaFiltro filtro, CancellationToken ct); Task<Result<IntegracaoSistemaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(IntegracaoSistemaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, IntegracaoSistemaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); Task<Result<object>> TestarDevAsync(long id, CancellationToken ct); }
public interface IWebhookRepository { Task<long> RegistrarRecebidoAsync(long? tenantId, string origem, WebhookReceberRequest request, string headersJson, string assinatura, bool? assinaturaValida, string? ip, string? userAgent, Guid correlationId, CancellationToken ct); Task<PagedResult<WebhookRecebidoResponse>> ListarRecebidosAsync(long tenantId, WebhookRecebidoFiltro filtro, CancellationToken ct); Task<PagedResult<WebhookEnviadoResponse>> ListarEnviadosAsync(long tenantId, WebhookRecebidoFiltro filtro, CancellationToken ct); Task<long> CriarEnviadoAsync(long tenantId, WebhookEnviarRequest request, long? usuarioId, CancellationToken ct); Task ReprocessarAsync(long tenantId, long id, CancellationToken ct); }
public interface IWebhookService { Task<Result<long>> ReceberAsync(string origem, WebhookReceberRequest request, IDictionary<string,string> headers, string? ip, string? userAgent, CancellationToken ct); Task<Result<PagedResult<WebhookRecebidoResponse>>> ListarRecebidosAsync(WebhookRecebidoFiltro filtro, CancellationToken ct); Task<Result<PagedResult<WebhookEnviadoResponse>>> ListarEnviadosAsync(WebhookRecebidoFiltro filtro, CancellationToken ct); Task<Result<long>> EnviarDevAsync(WebhookEnviarRequest request, CancellationToken ct); Task<Result> ReprocessarAsync(long id, CancellationToken ct); }
public interface IWebhookSignatureService { string Sign(string payload, string secret, DateTimeOffset? timestamp = null); bool Validate(string payload, string secret, string signature, DateTimeOffset? timestamp = null, TimeSpan? tolerance = null, DateTimeOffset? now = null); }
public interface IIdempotencyService { Task<Result<IdempotencyCheckResponse>> CheckAsync(IdempotencyCheckRequest request, CancellationToken ct); }
public interface IOutboxRepository { Task<PagedResult<OutboxEventoResponse>> ListarAsync(long tenantId, OutboxFiltro filtro, CancellationToken ct); Task<OutboxEventoResponse?> ObterAsync(long tenantId, long id, CancellationToken ct); Task<long> CriarAsync(long? tenantId, long? entidadeId, long? exercicioId, OutboxEventoCreateRequest request, long? usuarioId, CancellationToken ct); Task ReprocessarAsync(long tenantId, long id, CancellationToken ct); Task DeadLetterAsync(long tenantId, long id, string? erro, CancellationToken ct); Task CancelarAsync(long tenantId, long id, CancellationToken ct); }
public interface IOutboxService { Task<Result<PagedResult<OutboxEventoResponse>>> ListarAsync(OutboxFiltro filtro, CancellationToken ct); Task<Result<OutboxEventoResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(OutboxEventoCreateRequest request, CancellationToken ct); Task<Result> ReprocessarAsync(long id, ReprocessarOutboxRequest request, CancellationToken ct); Task<Result> DeadLetterAsync(long id, MoverDeadLetterRequest request, CancellationToken ct); Task<Result> CancelarAsync(long id, CancellationToken ct); }
public interface IIntegracaoLogRepository { Task RegistrarAsync(long? tenantId, string direcao, string tipoEvento, string status, string? requestResumoJson, string? responseResumoJson, Guid? correlationId, CancellationToken ct); }
public interface IIntegracaoLogService { Task RegistrarAsync(long? tenantId, string direcao, string tipoEvento, string status, object? request, object? response, Guid? correlationId, CancellationToken ct); }
public interface IRemessaOficialRepository { Task<PagedResult<RemessaOficialResponse>> ListarAsync(long tenantId, OutboxFiltro filtro, CancellationToken ct); Task<RemessaOficialResponse?> ObterAsync(long tenantId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, RemessaOficialCreateRequest request, long? usuarioId, CancellationToken ct); Task AtualizarStatusDevAsync(long tenantId, long id, string status, long? usuarioId, CancellationToken ct); }
public interface IRemessaOficialService { Task<Result<PagedResult<RemessaOficialResponse>>> ListarAsync(OutboxFiltro filtro, CancellationToken ct); Task<Result<RemessaOficialResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(RemessaOficialCreateRequest request, CancellationToken ct); Task<Result> GerarDevAsync(long id, GerarRemessaRequest request, CancellationToken ct); Task<Result> EnviarDevAsync(long id, EnviarRemessaDevRequest request, CancellationToken ct); Task<Result> CancelarAsync(long id, CancellationToken ct); }
public interface IGovBrAdapter { Task<Result<object>> TestarDevAsync(CancellationToken ct); }
public interface ICertificadoDigitalRepository { Task<PagedResult<CertificadoDigitalResponse>> ListarAsync(long tenantId, OutboxFiltro filtro, CancellationToken ct); Task<CertificadoDigitalResponse?> ObterAsync(long tenantId, long id, CancellationToken ct); Task<long> CriarAsync(long tenantId, CertificadoDigitalCreateRequest request, long? usuarioId, CancellationToken ct); Task RevogarAsync(long tenantId, long id, long? usuarioId, CancellationToken ct); }
public interface ICertificadoDigitalService { Task<Result<PagedResult<CertificadoDigitalResponse>>> ListarAsync(OutboxFiltro filtro, CancellationToken ct); Task<Result<CertificadoDigitalResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(CertificadoDigitalCreateRequest request, CancellationToken ct); Task<Result> RevogarAsync(long id, CancellationToken ct); }
public interface IAssinadorDigitalService { Task<Result<object>> ValidarEstruturaAsync(CancellationToken ct); Task<Result<object>> AssinarDevAsync(object request, CancellationToken ct); }
public interface IIntegracaoDashboardRepository { Task<IntegracaoDashboardResponse> ObterAsync(long tenantId, CancellationToken ct); }
public interface IIntegracaoDashboardService { Task<Result<IntegracaoDashboardResponse>> ObterAsync(CancellationToken ct); }
public interface IIntegracaoExportacaoRepository { Task<byte[]> ExportarAsync(long tenantId, string recurso, string formato, CancellationToken ct); }
public interface IIntegracaoExportacaoService { Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct); }

public abstract class IntegracaoServiceBase
{
    protected IntegracaoServiceBase(ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IAuditService audit) { Tenant = tenant; User = user; Permissions = permissions; Audit = audit; }
    protected ICurrentTenant Tenant { get; }
    protected ICurrentUser User { get; }
    protected IPermissionService Permissions { get; }
    protected IAuditService Audit { get; }
    protected long TenantId => Tenant.TenantId ?? 0;
    protected long? EntidadeId => Tenant.EntidadeId;
    protected long? ExercicioId => Tenant.ExercicioId;
    protected long? UsuarioId => User.UsuarioId;
    protected bool TenantValido => TenantId > 0;
    protected Result<T> TenantFailure<T>() => Result<T>.Failure("Tenant é obrigatório para integrações.");
    protected Result TenantFailure() => Result.Failure("Tenant é obrigatório para integrações.");
    protected async Task<bool> CanAsync(string chave, CancellationToken ct)
    {
        if (!User.UsuarioId.HasValue) return true;
        var parts = chave.Split('.');
        var recurso = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}" : chave;
        var acao = parts.Length >= 3 ? parts[2] : "visualizar";
        return await Permissions.HasPermissionAsync(User.UsuarioId.Value, IntegracaoPermissoes.Modulo, recurso, acao, ct).ConfigureAwait(false);
    }
    protected static string Json(object? value) => value is null ? "{}" : JsonSerializer.Serialize(value);
}
public sealed class ApiKeyHasher : IApiKeyHasher
{
    public string Algorithm => "PBKDF2-SHA256-100000";
    public string GenerateApiKey(bool production, out string prefix)
    {
        prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(6)).ToLowerInvariant();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace("+", "-", StringComparison.Ordinal).Replace("/", "_", StringComparison.Ordinal).TrimEnd('=');
        return $"sigov_{(production ? "live" : "dev")}_{prefix}_{secret}";
    }
    public string Hash(string apiKey)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(apiKey, salt, 100000, HashAlgorithmName.SHA256, 32);
        return $"{Algorithm}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public bool Verify(string apiKey, string storedHash)
    {
        var parts = storedHash.Split('$');
        if (parts.Length != 3 || parts[0] != Algorithm) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(apiKey, salt, 100000, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
public sealed class WebhookSignatureService : IWebhookSignatureService
{
    public string Sign(string payload, string secret, DateTimeOffset? timestamp = null)
    {
        var data = timestamp.HasValue ? $"{timestamp.Value.ToUnixTimeSeconds()}.{payload}" : payload;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
    public bool Validate(string payload, string secret, string signature, DateTimeOffset? timestamp = null, TimeSpan? tolerance = null, DateTimeOffset? now = null)
    {
        if (timestamp.HasValue && (now ?? DateTimeOffset.UtcNow) - timestamp.Value > (tolerance ?? TimeSpan.FromMinutes(5))) return false;
        var expected = Sign(payload, secret, timestamp);
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature ?? string.Empty));
    }
}
public sealed class ApiCredentialService : IntegracaoServiceBase, IApiCredentialService
{
    private readonly IApiCredentialRepository _repo; private readonly IApiKeyHasher _hasher; private readonly IHostEnvironment _env; private readonly ILogger<ApiCredentialService> _logger;
    public ApiCredentialService(IApiCredentialRepository repo, IApiKeyHasher hasher, IHostEnvironment env, ICurrentTenant tenant, ICurrentUser user, IPermissionService permissions, IAuditService audit, ILogger<ApiCredentialService> logger) : base(tenant,user,permissions,audit){_repo=repo;_hasher=hasher;_env=env;_logger=logger;}
    public async Task<Result<PagedResult<ApiCredentialResponse>>> ListarAsync(ApiCredentialFiltro filtro, CancellationToken ct){ if(!TenantValido)return TenantFailure<PagedResult<ApiCredentialResponse>>(); if(!await CanAsync(IntegracaoPermissoes.ApiCredentialVisualizar,ct).ConfigureAwait(false)) return Result<PagedResult<ApiCredentialResponse>>.Failure("403"); return Result<PagedResult<ApiCredentialResponse>>.Success(await _repo.ListarAsync(TenantId,filtro,ct).ConfigureAwait(false));}
    public async Task<Result<ApiCredentialResponse>> ObterAsync(long id, CancellationToken ct){ if(!TenantValido)return TenantFailure<ApiCredentialResponse>(); var r=await _repo.ObterAsync(TenantId,id,ct).ConfigureAwait(false); return r is null?Result<ApiCredentialResponse>.Failure("Credencial não encontrada."):Result<ApiCredentialResponse>.Success(r);}
    public async Task<Result<ApiCredentialCreateResponse>> CriarAsync(ApiCredentialCreateRequest request, CancellationToken ct){ if(!TenantValido)return TenantFailure<ApiCredentialCreateResponse>(); if(!await CanAsync(IntegracaoPermissoes.ApiCredentialCriar,ct).ConfigureAwait(false))return Result<ApiCredentialCreateResponse>.Failure("403"); try{var key=_hasher.GenerateApiKey(_env.IsProduction(),out var prefix); if(_env.IsProduction()&&key.StartsWith("sigov_dev_",StringComparison.Ordinal))return Result<ApiCredentialCreateResponse>.Failure("API key dev bloqueada em Production."); var hash=_hasher.Hash(key); var domain=ApiCredential.Create(TenantId,request.Nome,request.ClientId,prefix,hash); if(domain.IsFailure)return Result<ApiCredentialCreateResponse>.Failure(domain.Error??"Credencial inválida."); var id=await _repo.CriarAsync(TenantId,request,prefix,hash,_hasher.Algorithm,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","CRIAR","sigov.api_credential",id.ToString(CultureInfo.InvariantCulture),null,new{request.Nome,request.ClientId,ApiKey="***"},ct).ConfigureAwait(false); return Result<ApiCredentialCreateResponse>.Success(new ApiCredentialCreateResponse(id,request.ClientId,key,prefix,request.ExpiraAt));}catch(Exception ex){_logger.LogError(ex,"Erro ao criar API credential sem expor segredo."); return Result<ApiCredentialCreateResponse>.Failure("Erro ao criar credencial de API.");}}
    public async Task<Result> RevogarAsync(long id, RevogarApiCredentialRequest request, CancellationToken ct){return await Status(id,"REVOGADA","REVOGAR",request,ct).ConfigureAwait(false);} public Task<Result> SuspenderAsync(long id,CancellationToken ct)=>Status(id,"SUSPENSA","SUSPENDER",null,ct); public Task<Result> ReativarAsync(long id,CancellationToken ct)=>Status(id,"ATIVA","REATIVAR",null,ct);
    private async Task<Result> Status(long id,string status,string acao,object? novo,CancellationToken ct){ if(!TenantValido)return TenantFailure(); await _repo.AlterarStatusAsync(TenantId,id,status,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao",acao,"sigov.api_credential",id.ToString(CultureInfo.InvariantCulture),null,novo,ct).ConfigureAwait(false); return Result.Success();}
}
public sealed class IntegracaoSistemaService : IntegracaoServiceBase, IIntegracaoSistemaService
{
    private readonly IIntegracaoSistemaRepository _repo; private readonly IHostEnvironment _env; public IntegracaoSistemaService(IIntegracaoSistemaRepository repo,IHostEnvironment env,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;_env=env;}
    public async Task<Result<PagedResult<IntegracaoSistemaResponse>>> ListarAsync(IntegracaoSistemaFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<IntegracaoSistemaResponse>>(); return Result<PagedResult<IntegracaoSistemaResponse>>.Success(await _repo.ListarAsync(TenantId,f,ct).ConfigureAwait(false));}
    public async Task<Result<IntegracaoSistemaResponse>> ObterAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure<IntegracaoSistemaResponse>(); var r=await _repo.ObterAsync(TenantId,id,ct).ConfigureAwait(false); return r is null?Result<IntegracaoSistemaResponse>.Failure("Sistema não encontrado."):Result<IntegracaoSistemaResponse>.Success(r);}
    public async Task<Result<long>> CriarAsync(IntegracaoSistemaCreateRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure<long>(); if(!Enum.TryParse<IntegracaoTipo>(r.TipoIntegracao,true,out var tipo))return Result<long>.Failure("Tipo de integração inválido."); if(!Enum.TryParse<IntegracaoAmbiente>(r.Ambiente,true,out var amb))return Result<long>.Failure("Ambiente inválido."); var domain=IntegracaoSistema.Create(TenantId,r.Codigo,r.Nome,tipo,amb,false); if(domain.IsFailure)return Result<long>.Failure(domain.Error??"Integração inválida."); var id=await _repo.CriarAsync(TenantId,r,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","CRIAR","sigov.integracao_sistema",id.ToString(CultureInfo.InvariantCulture),null,r,ct).ConfigureAwait(false); return Result<long>.Success(id);}
    public async Task<Result> AtualizarAsync(long id,IntegracaoSistemaUpdateRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.AtualizarAsync(TenantId,id,r,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","EDITAR","sigov.integracao_sistema",id.ToString(CultureInfo.InvariantCulture),null,r,ct).ConfigureAwait(false); return Result.Success();}
    public async Task<Result> ExcluirAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.ExcluirAsync(TenantId,id,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","EXCLUIR","sigov.integracao_sistema",id.ToString(CultureInfo.InvariantCulture),null,new{isDeleted=true},ct).ConfigureAwait(false); return Result.Success();}
    public Task<Result<object>> TestarDevAsync(long id,CancellationToken ct){_ = id; _ = ct; return Task.FromResult(_env.IsProduction()?Result<object>.Failure("Adapter fake/dev bloqueado em Production."):Result<object>.Success(new{ok=true,adapter="dev",externalCall=false}));}
}
public sealed class WebhookService : IntegracaoServiceBase, IWebhookService
{
    private readonly IWebhookRepository _repo; private readonly IOutboxRepository _outbox; public WebhookService(IWebhookRepository repo,IOutboxRepository outbox,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;_outbox=outbox;}
    public async Task<Result<long>> ReceberAsync(string origem,WebhookReceberRequest r,IDictionary<string,string> headers,string? ip,string? userAgent,CancellationToken ct){var payload=Json(r.Payload); var domain=WebhookRecebido.Create(Tenant.TenantId,origem,r.Evento,payload,r.IdempotencyKey); if(domain.IsFailure)return Result<long>.Failure(domain.Error??"Webhook inválido."); var assinatura=headers.TryGetValue("X-Sigov-Signature",out var s)?s:string.Empty; var id=await _repo.RegistrarRecebidoAsync(Tenant.TenantId,origem,r,Json(headers),assinatura,null,ip,userAgent,Guid.NewGuid(),ct).ConfigureAwait(false); await _outbox.CriarAsync(Tenant.TenantId,EntidadeId,ExercicioId,new OutboxEventoCreateRequest("WebhookRecebido",new{id,origem,r.Evento},headers,origem,"sigov",r.IdempotencyKey,Guid.NewGuid()),UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","WEBHOOK_RECEBIDO","sigov.webhook_recebido",id.ToString(CultureInfo.InvariantCulture),null,new{origem,r.Evento},ct).ConfigureAwait(false); return Result<long>.Success(id);}
    public async Task<Result<PagedResult<WebhookRecebidoResponse>>> ListarRecebidosAsync(WebhookRecebidoFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<WebhookRecebidoResponse>>(); return Result<PagedResult<WebhookRecebidoResponse>>.Success(await _repo.ListarRecebidosAsync(TenantId,f,ct).ConfigureAwait(false));}
    public async Task<Result<PagedResult<WebhookEnviadoResponse>>> ListarEnviadosAsync(WebhookRecebidoFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<WebhookEnviadoResponse>>(); return Result<PagedResult<WebhookEnviadoResponse>>.Success(await _repo.ListarEnviadosAsync(TenantId,f,ct).ConfigureAwait(false));}
    public async Task<Result<long>> EnviarDevAsync(WebhookEnviarRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure<long>(); var domain=WebhookEnviado.Create(TenantId,r.Destino,r.Url,r.Evento,Json(r.Payload)); if(domain.IsFailure)return Result<long>.Failure(domain.Error??"Webhook inválido."); var id=await _repo.CriarEnviadoAsync(TenantId,r,UsuarioId,ct).ConfigureAwait(false); await _outbox.CriarAsync(TenantId,EntidadeId,ExercicioId,new OutboxEventoCreateRequest("WebhookEnviado",new{id,r.Evento},null,"sigov",r.Destino,null,Guid.NewGuid()),UsuarioId,ct).ConfigureAwait(false); return Result<long>.Success(id);}
    public async Task<Result> ReprocessarAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.ReprocessarAsync(TenantId,id,ct).ConfigureAwait(false); return Result.Success();}
}
public sealed class IdempotencyService : IntegracaoServiceBase, IIdempotencyService
{ public IdempotencyService(ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){} public Task<Result<IdempotencyCheckResponse>> CheckAsync(IdempotencyCheckRequest request,CancellationToken ct){_ = ct; var ok=!string.IsNullOrWhiteSpace(request.Chave)&&request.Ttl>TimeSpan.Zero; return Task.FromResult(Result<IdempotencyCheckResponse>.Success(new IdempotencyCheckResponse(ok,ok?"RESERVADA":"ERRO",ok?null:"Idempotency key inválida.")));}}
public sealed class OutboxService : IntegracaoServiceBase, IOutboxService
{ private readonly IOutboxRepository _repo; public OutboxService(IOutboxRepository repo,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;} public async Task<Result<PagedResult<OutboxEventoResponse>>> ListarAsync(OutboxFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<OutboxEventoResponse>>(); return Result<PagedResult<OutboxEventoResponse>>.Success(await _repo.ListarAsync(TenantId,f,ct).ConfigureAwait(false));} public async Task<Result<OutboxEventoResponse>> ObterAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure<OutboxEventoResponse>(); var r=await _repo.ObterAsync(TenantId,id,ct).ConfigureAwait(false); return r is null?Result<OutboxEventoResponse>.Failure("Evento não encontrado."):Result<OutboxEventoResponse>.Success(r);} public async Task<Result<long>> CriarAsync(OutboxEventoCreateRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure<long>(); var id=await _repo.CriarAsync(TenantId,EntidadeId,ExercicioId,r,UsuarioId,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","CRIAR","sigov.fila_evento",id.ToString(CultureInfo.InvariantCulture),null,r,ct).ConfigureAwait(false); return Result<long>.Success(id);} public async Task<Result> ReprocessarAsync(long id,ReprocessarOutboxRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.ReprocessarAsync(TenantId,id,ct).ConfigureAwait(false); await Audit.RegistrarAsync("integracao","REPROCESSAR","sigov.fila_evento",id.ToString(CultureInfo.InvariantCulture),null,r,ct).ConfigureAwait(false); return Result.Success();} public async Task<Result> DeadLetterAsync(long id,MoverDeadLetterRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.DeadLetterAsync(TenantId,id,r.Motivo,ct).ConfigureAwait(false); return Result.Success();} public async Task<Result> CancelarAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure(); await _repo.CancelarAsync(TenantId,id,ct).ConfigureAwait(false); return Result.Success();}}
public sealed class IntegracaoLogService : IIntegracaoLogService { private readonly IIntegracaoLogRepository _repo; public IntegracaoLogService(IIntegracaoLogRepository repo)=>_repo=repo; public Task RegistrarAsync(long? tenantId,string direcao,string tipoEvento,string status,object? request,object? response,Guid? correlationId,CancellationToken ct)=>_repo.RegistrarAsync(tenantId,direcao,tipoEvento,status,Json(request),Json(response),correlationId,ct); private static string? Json(object? value)=>value is null?null:System.Text.Json.JsonSerializer.Serialize(value); }
public sealed class RemessaOficialService : IntegracaoServiceBase, IRemessaOficialService
{ private readonly IRemessaOficialRepository _repo; private readonly IHostEnvironment _env; public RemessaOficialService(IRemessaOficialRepository repo,IHostEnvironment env,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;_env=env;} public async Task<Result<PagedResult<RemessaOficialResponse>>> ListarAsync(OutboxFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<RemessaOficialResponse>>(); return Result<PagedResult<RemessaOficialResponse>>.Success(await _repo.ListarAsync(TenantId,f,ct).ConfigureAwait(false));} public async Task<Result<RemessaOficialResponse>> ObterAsync(long id,CancellationToken ct){if(!TenantValido)return TenantFailure<RemessaOficialResponse>(); var r=await _repo.ObterAsync(TenantId,id,ct).ConfigureAwait(false); return r is null?Result<RemessaOficialResponse>.Failure("Remessa não encontrada."):Result<RemessaOficialResponse>.Success(r);} public async Task<Result<long>> CriarAsync(RemessaOficialCreateRequest r,CancellationToken ct){if(!TenantValido)return TenantFailure<long>(); if(!Enum.TryParse<RemessaOficialTipo>(r.TipoRemessa,true,out var tipo))return Result<long>.Failure("Tipo de remessa inválido."); var domain=RemessaOficial.Create(TenantId,tipo,r.Numero); if(domain.IsFailure)return Result<long>.Failure(domain.Error??"Remessa inválida."); var id=await _repo.CriarAsync(TenantId,EntidadeId,ExercicioId,r,UsuarioId,ct).ConfigureAwait(false); return Result<long>.Success(id);} public async Task<Result> GerarDevAsync(long id,GerarRemessaRequest r,CancellationToken ct){if(_env.IsProduction()||!r.DevAdapter)return Result.Failure("Adapter fake/dev bloqueado em Production ou não habilitado."); await _repo.AtualizarStatusDevAsync(TenantId,id,"GERADA",UsuarioId,ct).ConfigureAwait(false); return Result.Success();} public async Task<Result> EnviarDevAsync(long id,EnviarRemessaDevRequest r,CancellationToken ct){if(_env.IsProduction()||!r.DevAdapter)return Result.Failure("Adapter fake/dev bloqueado em Production ou não habilitado."); await _repo.AtualizarStatusDevAsync(TenantId,id,"ENVIADA_DEV",UsuarioId,ct).ConfigureAwait(false); return Result.Success();} public async Task<Result> CancelarAsync(long id,CancellationToken ct){await _repo.AtualizarStatusDevAsync(TenantId,id,"CANCELADA",UsuarioId,ct).ConfigureAwait(false); return Result.Success();}}
public sealed class CertificadoDigitalService : IntegracaoServiceBase, ICertificadoDigitalService
{ private readonly ICertificadoDigitalRepository _repo; public CertificadoDigitalService(ICertificadoDigitalRepository repo,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;} public async Task<Result<PagedResult<CertificadoDigitalResponse>>> ListarAsync(OutboxFiltro f,CancellationToken ct){if(!TenantValido)return TenantFailure<PagedResult<CertificadoDigitalResponse>>(); return Result<PagedResult<CertificadoDigitalResponse>>.Success(await _repo.ListarAsync(TenantId,f,ct).ConfigureAwait(false));} public async Task<Result<CertificadoDigitalResponse>> ObterAsync(long id,CancellationToken ct){var r=await _repo.ObterAsync(TenantId,id,ct).ConfigureAwait(false); return r is null?Result<CertificadoDigitalResponse>.Failure("Certificado não encontrado."):Result<CertificadoDigitalResponse>.Success(r);} public async Task<Result<long>> CriarAsync(CertificadoDigitalCreateRequest r,CancellationToken ct){var domain=CertificadoDigital.Create(TenantId,r.Nome,Enum.TryParse<CertificadoDigitalTipo>(r.TipoCertificado,true,out var tipo)?tipo:CertificadoDigitalTipo.ESTRUTURAL,r.ValidadeFim,r.StorageKey,Json(r.Metadados)); if(domain.IsFailure)return Result<long>.Failure(domain.Error??"Certificado inválido."); return Result<long>.Success(await _repo.CriarAsync(TenantId,r,UsuarioId,ct).ConfigureAwait(false));} public async Task<Result> RevogarAsync(long id,CancellationToken ct){await _repo.RevogarAsync(TenantId,id,UsuarioId,ct).ConfigureAwait(false); return Result.Success();}}
public sealed class GovBrEstruturalService : IGovBrAdapter { private readonly IHostEnvironment _env; public GovBrEstruturalService(IHostEnvironment env)=>_env=env; public Task<Result<object>> TestarDevAsync(CancellationToken ct){_ = ct; return Task.FromResult(_env.IsProduction()?Result<object>.Failure("Gov.br dev bloqueado em Production."):Result<object>.Success(new{provider="Gov.br estrutural",externalCall=false}));}}
public sealed class AssinadorDigitalEstruturalService : IAssinadorDigitalService { private readonly IHostEnvironment _env; public AssinadorDigitalEstruturalService(IHostEnvironment env)=>_env=env; public Task<Result<object>> ValidarEstruturaAsync(CancellationToken ct){_ = ct; return Task.FromResult(Result<object>.Success(new{estruturaValida=true,icpReal=false}));} public Task<Result<object>> AssinarDevAsync(object request,CancellationToken ct){_ = request; _ = ct; return Task.FromResult(_env.IsProduction()?Result<object>.Failure("Assinador dev bloqueado em Production."):Result<object>.Success(new{assinatura="estrutural-dev",externalCall=false}));}}
public sealed class IntegracaoDashboardService : IntegracaoServiceBase, IIntegracaoDashboardService { private readonly IIntegracaoDashboardRepository _repo; public IntegracaoDashboardService(IIntegracaoDashboardRepository repo,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;} public async Task<Result<IntegracaoDashboardResponse>> ObterAsync(CancellationToken ct){if(!TenantValido)return TenantFailure<IntegracaoDashboardResponse>(); return Result<IntegracaoDashboardResponse>.Success(await _repo.ObterAsync(TenantId,ct).ConfigureAwait(false));}}
public sealed class IntegracaoExportacaoService : IntegracaoServiceBase, IIntegracaoExportacaoService { private readonly IIntegracaoExportacaoRepository _repo; public IntegracaoExportacaoService(IIntegracaoExportacaoRepository repo,ICurrentTenant t,ICurrentUser u,IPermissionService p,IAuditService a):base(t,u,p,a){_repo=repo;} public async Task<Result<byte[]>> ExportarAsync(string recurso,string formato,CancellationToken ct){if(!TenantValido)return TenantFailure<byte[]>(); await Audit.RegistrarAsync("integracao","EXPORTAR",$"sigov.{recurso}",formato,null,new{recurso,formato},ct).ConfigureAwait(false); return Result<byte[]>.Success(await _repo.ExportarAsync(TenantId,recurso,formato,ct).ConfigureAwait(false));}}
public class TceAdapterDev : IGovBrAdapter { public Task<Result<object>> TestarDevAsync(CancellationToken ct){_ = ct; return Task.FromResult(Result<object>.Success(new{adapter="TCE dev",externalCall=false}));}}
public sealed class EsfingeAdapterDev : TceAdapterDev { }
public sealed class EsocialAdapterDev : TceAdapterDev { }
public sealed class EducacensoAdapterDev : TceAdapterDev { }
public sealed class EsusAdapterDev : TceAdapterDev { }
public sealed class AbrasfNfseAdapterDev : TceAdapterDev { }
public sealed class DesifAdapterDev : TceAdapterDev { }
public sealed class BancoArquivoAdapterDev : TceAdapterDev { }
public sealed class PixAdapterDev : TceAdapterDev { }
