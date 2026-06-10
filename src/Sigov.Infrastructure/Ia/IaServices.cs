using System.Text.RegularExpressions;
using Dapper;
using Sigov.Application.Ia;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Ia;

public sealed class IaMaskingService : IIaMaskingService
{
    private static readonly Regex Cpf = new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex Cnpj = new(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex Email = new(@"\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Phone = new(@"(?<!\d)(?:\+?55\s*)?(?:\(?\d{2}\)?\s*)?(?:9\s*)?\d{4}[-\s]?\d{4}(?!\d)", RegexOptions.Compiled);

    public string MaskSensitiveData(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var masked = Cnpj.Replace(value, "[CNPJ_MASCARADO]");
        masked = Cpf.Replace(masked, "[CPF_MASCARADO]");
        masked = Email.Replace(masked, "[EMAIL_MASCARADO]");
        return Phone.Replace(masked, "[TELEFONE_MASCARADO]");
    }
}

public sealed class InternalIaProviderClient : IIaProviderClient
{
    public Task<IaProviderResult> ExecuteAsync(IaProviderRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = request.Prompt ?? string.Empty;
        var lower = prompt.ToLowerInvariant();
        var fields = new Dictionary<string, string?>();
        var tipo = (request.Tipo ?? "ANALISE").ToUpperInvariant();
        string text;

        if (tipo.Contains("CLASSIFIC", StringComparison.OrdinalIgnoreCase) || lower.Contains("classificar"))
        {
            var classification = lower.Contains("contrato") || lower.Contains("vigência") || lower.Contains("cláusula") ? "CONTRATO" : lower.Contains("nota") && lower.Contains("serviço") ? "NOTA_FISCAL_SERVICO" : lower.Contains("despacho") || lower.Contains("processo") ? "PROCESSO_ADMINISTRATIVO" : "DOCUMENTO_GENERICO";
            fields["tipo_sugerido"] = classification;
            fields["confianca"] = classification == "DOCUMENTO_GENERICO" ? "0.6200" : "0.8700";
            text = $"Classificação sugerida: {classification}. Revise antes de aplicar em fluxos críticos.";
        }
        else if (tipo.Contains("EXTRAC", StringComparison.OrdinalIgnoreCase) || lower.Contains("extrair"))
        {
            fields["numero"] = FirstMatch(prompt, @"\b\d{3,}[/.-]?\d{0,4}\b");
            fields["valor"] = FirstMatch(prompt, @"R\$\s?\d+[\d\.,]*");
            fields["data"] = FirstMatch(prompt, @"\b\d{2}/\d{2}/\d{4}\b");
            text = "Campos estruturados extraídos por heurística interna. Valide campos com baixa confiança antes do uso oficial.";
        }
        else if (tipo.Contains("PRED", StringComparison.OrdinalIgnoreCase) || lower.Contains("inadimpl") || lower.Contains("ruptura") || lower.Contains("atraso"))
        {
            fields["score"] = lower.Contains("vencid") || lower.Contains("baixo") || lower.Contains("parad") ? "0.8200" : "0.4700";
            fields["classificacao"] = Convert.ToDecimal(fields["score"], System.Globalization.CultureInfo.InvariantCulture) >= 0.8m ? "ALTO" : "MEDIO";
            text = $"Predição inicial baseada em regras: risco {fields["classificacao"]} (score {fields["score"]}). Use como apoio, não como decisão autônoma.";
        }
        else if (tipo.Contains("RELATORIO", StringComparison.OrdinalIgnoreCase) || lower.Contains("relatório"))
        {
            text = $"Relatório assistido ({request.ModuloCodigo ?? "geral"}): principais pontos identificados, riscos operacionais, oportunidades de melhoria e próximos passos recomendados. Dados insuficientes devem ser complementados por usuário habilitado.";
        }
        else if (tipo.Contains("SUGEST", StringComparison.OrdinalIgnoreCase) || lower.Contains("suger"))
        {
            text = "Sugestão inteligente: priorize revisão humana, valide permissões, registre decisão e execute somente ações não críticas sem confirmação.";
        }
        else
        {
            var summary = prompt.Length <= 420 ? prompt : string.Concat(prompt.AsSpan(0, 420), "...");
            text = string.IsNullOrWhiteSpace(prompt) ? "Não há dados suficientes para gerar uma resposta confiável." : $"Resumo interno: {summary}\n\nObservação: resposta simulada pelo provider INTERNO, com dados limitados ao contexto informado.";
        }

        return Task.FromResult(new IaProviderResult(text, tipo, fields.ContainsKey("confianca") ? Convert.ToDecimal(fields["confianca"], System.Globalization.CultureInfo.InvariantCulture) : 0.75m, fields));
    }

    private static string? FirstMatch(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Value : null;
    }
}

public sealed class IaConsumptionService : IIaConsumptionService
{
    private readonly DapperContext _context;
    public IaConsumptionService(DapperContext context) => _context = context;

    public async Task RegistrarConsumoAsync(long tenantId, int tokensEntrada, int tokensSaida, decimal custoEstimado, CancellationToken cancellationToken = default)
    {
        const string sql = @"insert into sigov.ia_consumo(tenant_id,competencia,interacoes,tokens_entrada,tokens_saida,custo_estimado)
values(@TenantId,date_trunc('month', now())::date,1,@TokensEntrada,@TokensSaida,@CustoEstimado)
on conflict(tenant_id,competencia) do update set interacoes=sigov.ia_consumo.interacoes+1,tokens_entrada=sigov.ia_consumo.tokens_entrada+excluded.tokens_entrada,tokens_saida=sigov.ia_consumo.tokens_saida+excluded.tokens_saida,custo_estimado=sigov.ia_consumo.custo_estimado+excluded.custo_estimado;";
        using var connection = _context.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, TokensEntrada = tokensEntrada, TokensSaida = tokensSaida, CustoEstimado = custoEstimado }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(@"insert into sigov.tenant_uso_mensal(tenant_id,ano,mes,ia_interacoes,ia_tokens_entrada,ia_tokens_saida)
values(@TenantId,extract(year from now())::int,extract(month from now())::int,1,@TokensEntrada,@TokensSaida)
on conflict(tenant_id,ano,mes) do update set ia_interacoes=sigov.tenant_uso_mensal.ia_interacoes+1,ia_tokens_entrada=sigov.tenant_uso_mensal.ia_tokens_entrada+excluded.ia_tokens_entrada,ia_tokens_saida=sigov.tenant_uso_mensal.ia_tokens_saida+excluded.ia_tokens_saida,updated_at=now();", new { TenantId = tenantId, TokensEntrada = tokensEntrada, TokensSaida = tokensSaida }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class IaSuggestionService : IIaSuggestionService
{
    private readonly DapperContext _context;
    public IaSuggestionService(DapperContext context) => _context = context;

    public async Task<long> CriarSugestaoAsync(long tenantId, long? execucaoId, string? moduloCodigo, string titulo, string descricao, string tipo, string prioridade, bool exigeConfirmacao, CancellationToken cancellationToken = default)
    {
        const string sql = @"insert into sigov.ia_sugestao(tenant_id,execucao_id,modulo_codigo,titulo,descricao,tipo,prioridade,exige_confirmacao)
values(@TenantId,@ExecucaoId,@ModuloCodigo,@Titulo,@Descricao,@Tipo,@Prioridade,@ExigeConfirmacao) returning id;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, ExecucaoId = execucaoId, ModuloCodigo = moduloCodigo, Titulo = titulo, Descricao = descricao, Tipo = tipo, Prioridade = prioridade, ExigeConfirmacao = exigeConfirmacao }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class IaAutomationService : IIaAutomationService
{
    private readonly DapperContext _context;
    public IaAutomationService(DapperContext context) => _context = context;

    public async Task<long> ExecutarAsync(long tenantId, long automacaoId, Guid correlationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"insert into sigov.ia_automacao_execucao(automacao_id,tenant_id,status,entrada_json,resultado_json,correlation_id,concluida_at)
select id, tenant_id, case when exige_confirmacao then 'PENDENTE_APROVACAO' else 'CONCLUIDA' end, condicao_json, jsonb_build_object('mensagem','Execução interna registrada; ações críticas aguardam confirmação humana.'), @CorrelationId, case when exige_confirmacao then null else now() end
from sigov.ia_automacao where id=@AutomacaoId and tenant_id=@TenantId returning id;";
        using var connection = _context.CreateConnection();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, new { TenantId = tenantId, AutomacaoId = automacaoId, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class IaExecutionService : IIaExecutionService
{
    private readonly DapperContext _context;
    private readonly IIaProviderClient _provider;
    private readonly IIaMaskingService _masking;
    private readonly IIaConsumptionService _consumption;

    public IaExecutionService(DapperContext context, IIaProviderClient provider, IIaMaskingService masking, IIaConsumptionService consumption)
    {
        _context = context;
        _provider = provider;
        _masking = masking;
        _consumption = consumption;
    }

    public async Task<IaExecutionResult> ExecuteAsync(long tenantId, long? usuarioId, IaExecutionRequest request, Guid correlationId, CancellationToken cancellationToken = default)
    {
        using var connection = _context.CreateConnection();
        var cfg = await connection.QuerySingleOrDefaultAsync<ConfigRow>(new CommandDefinition("select ia_habilitada as IaHabilitada, permitir_envio_externo as PermitirEnvioExterno, mascarar_dados_sensiveis as MascararDadosSensiveis, limite_interacoes_mes as LimiteInteracoesMes, limite_tokens_mes as LimiteTokensMes from sigov.ia_configuracao_tenant where tenant_id=@TenantId", new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false) ?? new ConfigRow(false, false, true, null, null);
        if (!cfg.IaHabilitada) throw new InvalidOperationException("IA não habilitada para o tenant.");
        var consumo = await connection.QuerySingleOrDefaultAsync<ConsumoRow>(new CommandDefinition("select interacoes as Interacoes, tokens_entrada + tokens_saida as Tokens from sigov.ia_consumo where tenant_id=@TenantId and competencia=date_trunc('month', now())::date", new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false) ?? new ConsumoRow(0, 0);
        if ((cfg.LimiteInteracoesMes.HasValue && consumo.Interacoes >= cfg.LimiteInteracoesMes.Value) || (cfg.LimiteTokensMes.HasValue && consumo.Tokens >= cfg.LimiteTokensMes.Value)) throw new InvalidOperationException("Limite mensal de IA excedido para o tenant.");

        var prompt = cfg.MascararDadosSensiveis ? _masking.MaskSensitiveData(request.Prompt) : request.Prompt;
        var execucaoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"insert into sigov.ia_execucao(tenant_id,usuario_id,assistente_codigo,modulo_codigo,tipo,origem,origem_id,prompt,status,provedor_codigo,correlation_id)
values(@TenantId,@UsuarioId,@AssistenteCodigo,@ModuloCodigo,@Tipo,@Origem,@OrigemId,@Prompt,'PROCESSANDO','INTERNO',@CorrelationId) returning id;", new { TenantId = tenantId, UsuarioId = usuarioId, request.AssistenteCodigo, request.ModuloCodigo, request.Tipo, request.Origem, request.OrigemId, Prompt = prompt, CorrelationId = correlationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (request.Contexto is not null)
        {
            foreach (var item in request.Contexto)
            {
                var sensitive = IsSensitive(item.Key) || IsSensitive(item.Value);
                await connection.ExecuteAsync(new CommandDefinition("insert into sigov.ia_execucao_contexto(execucao_id,chave,valor,sensivel,mascarado) values(@ExecucaoId,@Chave,@Valor,@Sensivel,@Mascarado);", new { ExecucaoId = execucaoId, Chave = item.Key, Valor = sensitive && cfg.MascararDadosSensiveis ? _masking.MaskSensitiveData(item.Value ?? string.Empty) : item.Value, Sensivel = sensitive, Mascarado = sensitive && cfg.MascararDadosSensiveis }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            }
        }

        try
        {
            var providerResult = await _provider.ExecuteAsync(new IaProviderRequest(request.Tipo, prompt, request.ModuloCodigo, request.Contexto ?? new Dictionary<string, string?>()), cancellationToken).ConfigureAwait(false);
            var tokensIn = EstimateTokens(prompt);
            var tokensOut = EstimateTokens(providerResult.Text);
            var cost = decimal.Round((tokensIn + tokensOut) * 0.00001m, 6);
            await connection.ExecuteAsync(new CommandDefinition("update sigov.ia_execucao set resposta=@Resposta,status='CONCLUIDA',tokens_entrada=@TokensIn,tokens_saida=@TokensOut,custo_estimado=@Cost,concluida_at=now() where id=@Id and tenant_id=@TenantId", new { Id = execucaoId, TenantId = tenantId, Resposta = providerResult.Text, TokensIn = tokensIn, TokensOut = tokensOut, Cost = cost }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            await _consumption.RegistrarConsumoAsync(tenantId, tokensIn, tokensOut, cost, cancellationToken).ConfigureAwait(false);
            return new IaExecutionResult(execucaoId, providerResult.Text, "CONCLUIDA", "INTERNO", tokensIn, tokensOut, cost, correlationId);
        }
        catch (Exception ex)
        {
            await connection.ExecuteAsync(new CommandDefinition("update sigov.ia_execucao set status='FALHOU',erro=@Erro,concluida_at=now() where id=@Id and tenant_id=@TenantId", new { Id = execucaoId, TenantId = tenantId, Erro = ex.Message }, cancellationToken: cancellationToken)).ConfigureAwait(false);
            throw;
        }
    }

    private static int EstimateTokens(string? text) => Math.Max(1, (int)Math.Ceiling((text ?? string.Empty).Length / 4m));
    private static bool IsSensitive(string? text) => !string.IsNullOrWhiteSpace(text) && (text.Contains("cpf", StringComparison.OrdinalIgnoreCase) || text.Contains("cnpj", StringComparison.OrdinalIgnoreCase) || text.Contains("email", StringComparison.OrdinalIgnoreCase) || text.Contains("telefone", StringComparison.OrdinalIgnoreCase));
    private sealed record ConfigRow(bool IaHabilitada, bool PermitirEnvioExterno, bool MascararDadosSensiveis, int? LimiteInteracoesMes, int? LimiteTokensMes);
    private sealed record ConsumoRow(int Interacoes, int Tokens);
}
