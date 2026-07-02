using System.Text.RegularExpressions;
using Dapper;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Web.Services;

public sealed record AiStatusViewModel(bool Configured, bool Enabled, string Provider, string Model, int MonthlyLimit, int CurrentUsage, string LgpdPolicy, IReadOnlyList<string> AllowedModules, IReadOnlyList<AiAssistantItem> Assistants, IReadOnlyList<AiLogItem> Logs, string FallbackMessage);
public sealed record AiAssistantItem(string Name, string Module, string Status, string Description);
public sealed record AiLogItem(DateTimeOffset CreatedAt, string User, string Module, string Action, string PromptMasked, string ResponseSummary, string Status);
public sealed record AiRequestViewModel(string? Modulo, string? Acao, string? Contexto, string? Justificativa);
public sealed record AiSuggestionResult(string Status, string Message, string Disclaimer, bool Demonstrative, string PromptMasked);

public sealed class AiConfigurationService
{
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly ILogger<AiConfigurationService> _logger;

    public AiConfigurationService(NpgsqlConnectionFactory connectionFactory, IDatabaseSchemaInspector schemaInspector, ILogger<AiConfigurationService> logger)
    {
        _connectionFactory = connectionFactory;
        _schemaInspector = schemaInspector;
        _logger = logger;
    }

    public async Task<AiStatusViewModel> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var configured = await _schemaInspector.TableExistsAsync("sigov", "ai_configuracao", cancellationToken).ConfigureAwait(false);
            var logs = await LoadLogsAsync(cancellationToken).ConfigureAwait(false);
            return new AiStatusViewModel(
                configured,
                false,
                configured ? "Configurado no schema" : "Não configurado",
                configured ? "Consultar sigov.ai_configuracao" : "N/A",
                0,
                logs.Count,
                "Mascaramento obrigatório de CPF/CNPJ/e-mail/telefone antes de envio externo.",
                new[] { "Protocolo", "GED", "Jurídico", "Tributário", "Contratos", "Financeiro", "Workflow", "Tarefas", "Dashboard", "Minha Central" },
                BuildAssistants(),
                logs,
                configured ? "IA exige provider, modelo, segredo seguro e política LGPD ativa antes de executar." : "Assistente inteligente não configurado neste ambiente.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha controlada ao obter status de IA.");
            return new AiStatusViewModel(false, false, "Indisponível", "N/A", 0, 0, "LGPD em fallback seguro.", Array.Empty<string>(), BuildAssistants(), Array.Empty<AiLogItem>(), "Não foi possível consultar a configuração de IA; nenhuma chamada externa foi realizada.");
        }
    }

    private async Task<IReadOnlyList<AiLogItem>> LoadLogsAsync(CancellationToken cancellationToken)
    {
        if (!await _schemaInspector.TableExistsAsync("sigov", "ai_log", cancellationToken).ConfigureAwait(false)) return Array.Empty<AiLogItem>();
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"select coalesce(created_at, now()) as CreatedAt, coalesce(usuario_id::text,'sistema') as User, coalesce(modulo,'geral') as Module, coalesce(acao,'consulta') as Action, coalesce(prompt_mascarado,'') as PromptMasked, coalesce(resposta_resumida,'') as ResponseSummary, coalesce(status,'registrado') as Status from sigov.ai_log order by created_at desc limit 20";
            var rows = await connection.QueryAsync<AiLogItem>(new CommandDefinition(sql, cancellationToken: cancellationToken)).ConfigureAwait(false);
            return rows.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Logs de IA indisponíveis; usando fallback honesto.");
            return Array.Empty<AiLogItem>();
        }
    }

    private static IReadOnlyList<AiAssistantItem> BuildAssistants() => new[]
    {
        new AiAssistantItem("Assistente de Protocolo", "Protocolo", "Em governança", "Sugere tramitação, pendências e despacho base sem decisão automática."),
        new AiAssistantItem("Assistente de GED", "GED/OCR", "Em governança", "Classifica documentos e identifica sensibilidade com revisão humana."),
        new AiAssistantItem("Assistente Jurídico", "Jurídico", "Em governança", "Apoia resumos e checklists, sem substituir parecer humano."),
        new AiAssistantItem("Assistente Tributário", "Tributário", "Em governança", "Apoia análise operacional respeitando LGPD e auditoria."),
        new AiAssistantItem("Assistente de Contratos", "Contratos", "Em governança", "Resume cláusulas, prazos e riscos com confirmação do usuário."),
        new AiAssistantItem("Assistente Financeiro", "Financeiro", "Em governança", "Apoia alertas e próximos passos sem executar ações."),
        new AiAssistantItem("Assistente Administrativo", "Workflow/Tarefas", "Em governança", "Sugere tarefa, prazo e checklist como regra do sistema quando IA estiver indisponível.")
    };
}

public sealed class AiAssistantService
{
    private readonly AiConfigurationService _configurationService;
    private readonly AiAuditService _auditService;
    private readonly ILogger<AiAssistantService> _logger;
    public AiAssistantService(AiConfigurationService configurationService, AiAuditService auditService, ILogger<AiAssistantService> logger) { _configurationService = configurationService; _auditService = auditService; _logger = logger; }

    public async Task<AiSuggestionResult> SuggestAsync(AiRequestViewModel request, string userAgent, string ip, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _configurationService.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            var prompt = MaskSensitiveData($"{request.Modulo} {request.Acao} {request.Contexto} {request.Justificativa}");
            var result = status.Configured && status.Enabled
                ? new AiSuggestionResult("Configurado", "Provider configurado, porém a execução externa deve ser implementada com segredo seguro antes de enviar dados reais.", "Revise a sugestão antes de usar. A decisão final é do usuário.", false, prompt)
                : new AiSuggestionResult("Fallback honesto", "Assistente inteligente não configurado neste ambiente.", "Revise a sugestão antes de usar. A decisão final é do usuário.", true, prompt);
            await _auditService.LogAsync(request.Modulo ?? "geral", request.Acao ?? "sugerir", prompt, result.Message, result.Status, ip, userAgent, correlationId, cancellationToken).ConfigureAwait(false);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha controlada no assistente IA.");
            return new AiSuggestionResult("Erro tratado", "Não foi possível acionar o assistente. Nenhuma decisão foi tomada automaticamente.", "Revise a sugestão antes de usar. A decisão final é do usuário.", true, string.Empty);
        }
    }

    public static string MaskSensitiveData(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var value = Regex.Replace(input, @"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", "***.***.***-**");
        value = Regex.Replace(value, @"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", "**.***.***/****-**");
        value = Regex.Replace(value, @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}", "***@***", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, @"\b(?:\+?55\s?)?(?:\(?\d{2}\)?\s?)?9?\d{4}[-\s]?\d{4}\b", "(**) *****-****");
        return value;
    }
}

public sealed class AiAuditService
{
    private readonly IAuditTrailService _auditTrail;
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly NpgsqlConnectionFactory _connectionFactory;
    private readonly ILogger<AiAuditService> _logger;
    public AiAuditService(IAuditTrailService auditTrail, IDatabaseSchemaInspector schemaInspector, NpgsqlConnectionFactory connectionFactory, ILogger<AiAuditService> logger) { _auditTrail = auditTrail; _schemaInspector = schemaInspector; _connectionFactory = connectionFactory; _logger = logger; }
    public async Task LogAsync(string module, string action, string promptMasked, string responseSummary, string status, string ip, string userAgent, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            await _auditTrail.RegistrarAsync(null, null, $"ia.{action}", "ai_log", null, null, new { module, promptMasked, responseSummary, status }, ip, userAgent, correlationId, cancellationToken).ConfigureAwait(false);
            if (!await _schemaInspector.TableExistsAsync("sigov", "ai_log", cancellationToken).ConfigureAwait(false)) return;
            using var connection = _connectionFactory.CreateConnection();
            const string sql = @"insert into sigov.ai_log (modulo, acao, prompt_mascarado, resposta_resumida, status, created_at) values (@Module, @Action, @PromptMasked, @ResponseSummary, @Status, now())";
            await connection.ExecuteAsync(new CommandDefinition(sql, new { Module = module, Action = action, PromptMasked = promptMasked, ResponseSummary = responseSummary, Status = status }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Log de IA não persistido; evento mantido em log estruturado."); }
    }
}
