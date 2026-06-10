namespace Sigov.Application.Ia;

public sealed record IaConfiguracaoTenantResponse(long TenantId, bool IaHabilitada, bool PermitirEnvioExterno, bool MascararDadosSensiveis, bool ExigirConfirmacaoAcaoCritica, string? ProvedorPadraoCodigo, int? LimiteInteracoesMes, int? LimiteTokensMes, DateTimeOffset? UpdatedAt);
public sealed record IaConfiguracaoTenantRequest(bool IaHabilitada, bool PermitirEnvioExterno, bool MascararDadosSensiveis, bool ExigirConfirmacaoAcaoCritica, string? ProvedorPadraoCodigo, int? LimiteInteracoesMes, int? LimiteTokensMes);
public sealed record IaExecutionRequest(string Tipo, string Prompt, string? ModuloCodigo = null, string? AssistenteCodigo = null, string? Origem = null, long? OrigemId = null, IReadOnlyDictionary<string, string?>? Contexto = null, bool Critica = false);
public sealed record IaExecutionResult(long ExecucaoId, string Resposta, string Status, string ProvedorCodigo, int TokensEntrada, int TokensSaida, decimal CustoEstimado, Guid CorrelationId);
public sealed record IaProviderRequest(string Tipo, string Prompt, string? ModuloCodigo, IReadOnlyDictionary<string, string?> Contexto);
public sealed record IaProviderResult(string Text, string Kind, decimal Confidence, IReadOnlyDictionary<string, string?> Fields);
public sealed record IaAutomationRequest(string Codigo, string Nome, string? Descricao, string? ModuloCodigo, string Gatilho, string? CondicaoJson, string AcaoJson, bool ExigeConfirmacao, bool Ativo);
public sealed record IaFeedbackRequest(long? ExecucaoId, long? SugestaoId, int? Avaliacao, string? Comentario, bool? Util);
public sealed record IaRelatorioRequest(string Tipo, string? ModuloCodigo, DateTime? Inicio, DateTime? Fim, string? Contexto);
public sealed record IaPredicaoRequest(string? Origem, long? OrigemId, decimal? Valor, int? Dias, string? Contexto);

public interface IIaMaskingService
{
    string MaskSensitiveData(string value);
}

public interface IIaProviderClient
{
    Task<IaProviderResult> ExecuteAsync(IaProviderRequest request, CancellationToken cancellationToken = default);
}

public interface IIaConsumptionService
{
    Task RegistrarConsumoAsync(long tenantId, int tokensEntrada, int tokensSaida, decimal custoEstimado, CancellationToken cancellationToken = default);
}

public interface IIaExecutionService
{
    Task<IaExecutionResult> ExecuteAsync(long tenantId, long? usuarioId, IaExecutionRequest request, Guid correlationId, CancellationToken cancellationToken = default);
}

public interface IIaSuggestionService
{
    Task<long> CriarSugestaoAsync(long tenantId, long? execucaoId, string? moduloCodigo, string titulo, string descricao, string tipo, string prioridade, bool exigeConfirmacao, CancellationToken cancellationToken = default);
}

public interface IIaAutomationService
{
    Task<long> ExecutarAsync(long tenantId, long automacaoId, Guid correlationId, CancellationToken cancellationToken = default);
}
