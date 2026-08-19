using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Ia;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ia")]
[RequireModule("ia_assistente")]
public sealed class IaController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IIaExecutionService _execution;
    private readonly IIaAutomationService _automations;
    private readonly ILogger<IaController> _logger;

    public IaController(DapperContext context, ICurrentTenant tenant, ICurrentUser user, IIaExecutionService execution, IIaAutomationService automations, ILogger<IaController> logger)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _execution = execution;
        _automations = automations;
        _logger = logger;
    }

    [HttpGet("configuracao")]
    public async Task<ActionResult<ApiResponse<object>>> GetConfiguracao(CancellationToken ct) => await Safe("IA_CONFIGURACAO_VISUALIZADA", async (tenantId, cid) =>
    {
        using var c = _context.CreateConnection();
        var row = await c.QuerySingleOrDefaultAsync<object>(new CommandDefinition("select tenant_id, ia_habilitada, permitir_envio_externo, mascarar_dados_sensiveis, exigir_confirmacao_acao_critica, provedor_padrao_codigo, limite_interacoes_mes, limite_tokens_mes, updated_at from sigov.ia_configuracao_tenant where tenant_id=@TenantId", new { TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false)
            ?? new { tenant_id = tenantId, ia_habilitada = false, permitir_envio_externo = false, mascarar_dados_sensiveis = true, exigir_confirmacao_acao_critica = true, provedor_padrao_codigo = "INTERNO", limite_interacoes_mes = (int?)null, limite_tokens_mes = (int?)null };
        return Ok(ApiResponse<object>.Ok(row, correlationId: cid));
    }, ct).ConfigureAwait(false);

    [HttpPut("configuracao")]
    public async Task<ActionResult<ApiResponse<object>>> PutConfiguracao([FromBody] IaConfiguracaoTenantRequest request, CancellationToken ct) => await Safe("IA_CONFIGURACAO_ATUALIZADA", async (tenantId, cid) =>
    {
        using var c = _context.CreateConnection();
        var row = await c.QuerySingleAsync<object>(new CommandDefinition(@"insert into sigov.ia_configuracao_tenant(tenant_id,ia_habilitada,permitir_envio_externo,mascarar_dados_sensiveis,exigir_confirmacao_acao_critica,provedor_padrao_codigo,limite_interacoes_mes,limite_tokens_mes,updated_at)
values(@TenantId,@IaHabilitada,@PermitirEnvioExterno,@MascararDadosSensiveis,@ExigirConfirmacaoAcaoCritica,coalesce(@ProvedorPadraoCodigo,'INTERNO'),@LimiteInteracoesMes,@LimiteTokensMes,now())
on conflict(tenant_id) do update set ia_habilitada=excluded.ia_habilitada,permitir_envio_externo=excluded.permitir_envio_externo,mascarar_dados_sensiveis=excluded.mascarar_dados_sensiveis,exigir_confirmacao_acao_critica=excluded.exigir_confirmacao_acao_critica,provedor_padrao_codigo=excluded.provedor_padrao_codigo,limite_interacoes_mes=excluded.limite_interacoes_mes,limite_tokens_mes=excluded.limite_tokens_mes,updated_at=now() returning *;", new { TenantId = tenantId, request.IaHabilitada, request.PermitirEnvioExterno, request.MascararDadosSensiveis, request.ExigirConfirmacaoAcaoCritica, request.ProvedorPadraoCodigo, request.LimiteInteracoesMes, request.LimiteTokensMes }, cancellationToken: ct)).ConfigureAwait(false);
        await Audit(c, tenantId, "IA_CONFIGURACAO_ATUALIZADA", null, null, request, cid, ct).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(row, "Configuração de IA atualizada.", cid));
    }, ct).ConfigureAwait(false);

    [HttpGet("assistentes")]
    public async Task<ActionResult<ApiResponse<object>>> Assistentes(CancellationToken ct) => await Query("select id, codigo, nome, descricao, tipo, ativo, created_at from sigov.ia_assistente where ativo=true order by nome", "IA_ASSISTENTES_LISTADOS", ct).ConfigureAwait(false);

    [HttpGet("assistentes/{codigo}")]
    public async Task<ActionResult<ApiResponse<object>>> Assistente(string codigo, CancellationToken ct) => await Safe("IA_ASSISTENTE_OBTIDO", async (tenantId, cid) =>
    {
        using var c = _context.CreateConnection();
        var row = await c.QuerySingleOrDefaultAsync<object>(new CommandDefinition("select id, codigo, nome, descricao, tipo, ativo, created_at from sigov.ia_assistente where codigo=@Codigo", new { Codigo = codigo }, cancellationToken: ct)).ConfigureAwait(false);
        return row is null ? NotFound(ApiResponse<object>.Fail("Assistente não encontrado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid));
    }, ct).ConfigureAwait(false);

    [HttpPost("executar")]
    public async Task<ActionResult<ApiResponse<object>>> Executar([FromBody] IaExecutionRequest request, CancellationToken ct) => await Safe("IA_EXECUCAO_CRIADA", async (tenantId, cid) =>
    {
        var result = await _execution.ExecuteAsync(tenantId, UserId(), request, Guid.Parse(cid), ct).ConfigureAwait(false);
        using var c = _context.CreateConnection();
        await Audit(c, tenantId, "IA_EXECUCAO_CONCLUIDA", "ia_execucao", result.ExecucaoId, request, cid, ct).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(result, correlationId: cid));
    }, ct).ConfigureAwait(false);

    [HttpGet("execucoes")]
    public async Task<ActionResult<ApiResponse<object>>> Execucoes(CancellationToken ct) => await TenantQuery("select id,tenant_id,usuario_id,assistente_codigo,modulo_codigo,tipo,origem,origem_id,status,provedor_codigo,tokens_entrada,tokens_saida,custo_estimado,correlation_id,created_at,concluida_at from sigov.ia_execucao where tenant_id=@TenantId order by created_at desc limit 100", "IA_EXECUCOES_LISTADAS", ct).ConfigureAwait(false);

    [HttpGet("execucoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Execucao(long id, CancellationToken ct) => await TenantSingle("select id, tenant_id, usuario_id, assistente_codigo, modulo_codigo, tipo, origem, origem_id, prompt, resposta, status, provedor_codigo, tokens_entrada, tokens_saida, custo_estimado, erro, correlation_id, created_at, concluida_at from sigov.ia_execucao where tenant_id=@TenantId and id=@Id", new { Id = id }, "Execução não encontrada.", ct).ConfigureAwait(false);

    [HttpPost("execucoes/{id:long}/cancelar")]
    public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id, CancellationToken ct) => await UpdateStatus("ia_execucao", id, "CANCELADA", "Execução cancelada.", ct).ConfigureAwait(false);

    [HttpGet("sugestoes")]
    public async Task<ActionResult<ApiResponse<object>>> Sugestoes(CancellationToken ct) => await TenantQuery("select id, tenant_id, execucao_id, modulo_codigo, origem, origem_id, titulo, descricao, tipo, prioridade, status, exige_confirmacao, criada_at, aplicada_at, rejeitada_at, usuario_decisao_id from sigov.ia_sugestao where tenant_id=@TenantId order by criada_at desc limit 100", "IA_SUGESTOES_LISTADAS", ct).ConfigureAwait(false);
    [HttpGet("sugestoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Sugestao(long id, CancellationToken ct) => await TenantSingle("select id, tenant_id, execucao_id, modulo_codigo, origem, origem_id, titulo, descricao, tipo, prioridade, status, exige_confirmacao, criada_at, aplicada_at, rejeitada_at, usuario_decisao_id from sigov.ia_sugestao where tenant_id=@TenantId and id=@Id", new { Id = id }, "Sugestão não encontrada.", ct).ConfigureAwait(false);
    [HttpPost("sugestoes/{id:long}/aprovar")]
    public async Task<ActionResult<ApiResponse<object>>> AprovarSugestao(long id, CancellationToken ct) => await SuggestionStatus(id, "APROVADA", "aplicada_at=null, rejeitada_at=null", "IA_SUGESTAO_APROVADA", ct).ConfigureAwait(false);
    [HttpPost("sugestoes/{id:long}/aplicar")]
    public async Task<ActionResult<ApiResponse<object>>> AplicarSugestao(long id, CancellationToken ct) => await SuggestionStatus(id, "APLICADA", "aplicada_at=now()", "IA_SUGESTAO_APLICADA", ct).ConfigureAwait(false);
    [HttpPost("sugestoes/{id:long}/rejeitar")]
    public async Task<ActionResult<ApiResponse<object>>> RejeitarSugestao(long id, CancellationToken ct) => await SuggestionStatus(id, "REJEITADA", "rejeitada_at=now()", "IA_SUGESTAO_REJEITADA", ct).ConfigureAwait(false);

    [HttpPost("documentos/{documentoId:long}/resumir")]
    public async Task<ActionResult<ApiResponse<object>>> ResumirDocumento(long documentoId, [FromBody] IaExecutionRequest? request, CancellationToken ct) => await Executar(request ?? new IaExecutionRequest("RESUMO", $"Resumir documento {documentoId}", "ged", "ASSISTENTE_GED", "DOCUMENTO", documentoId), ct).ConfigureAwait(false);
    [HttpPost("documentos/{documentoId:long}/classificar")]
    public async Task<ActionResult<ApiResponse<object>>> ClassificarDocumento(long documentoId, [FromBody] IaExecutionRequest? request, CancellationToken ct) => await DocumentAction(documentoId, request ?? new IaExecutionRequest("CLASSIFICACAO", $"Classificar documento {documentoId}", "ged", "ASSISTENTE_GED", "DOCUMENTO", documentoId), "IA_DOCUMENTO_CLASSIFICADO", ct).ConfigureAwait(false);
    [HttpPost("documentos/{documentoId:long}/extrair-campos")]
    public async Task<ActionResult<ApiResponse<object>>> ExtrairCampos(long documentoId, [FromBody] IaExecutionRequest? request, CancellationToken ct) => await DocumentAction(documentoId, request ?? new IaExecutionRequest("EXTRACAO", $"Extrair campos do documento {documentoId}", "ged", "ASSISTENTE_GED", "DOCUMENTO", documentoId), "IA_CAMPOS_EXTRAIDOS", ct).ConfigureAwait(false);
    [HttpGet("documentos/{documentoId:long}/classificacoes")]
    public async Task<ActionResult<ApiResponse<object>>> Classificacoes(long documentoId, CancellationToken ct) => await TenantQuery("select id, tenant_id, documento_id, tipo_sugerido, confianca, metadados_json, revisado, revisado_por, created_at from sigov.ia_classificacao_documento where tenant_id=@TenantId and documento_id=@DocumentoId order by created_at desc", "IA_CLASSIFICACOES_LISTADAS", ct, new { DocumentoId = documentoId }).ConfigureAwait(false);
    [HttpGet("documentos/{documentoId:long}/campos-extraidos")]
    public async Task<ActionResult<ApiResponse<object>>> Campos(long documentoId, CancellationToken ct) => await TenantQuery("select id, tenant_id, documento_id, campo, valor, confianca, revisado, revisado_por, created_at from sigov.ia_extracao_campo where tenant_id=@TenantId and documento_id=@DocumentoId order by created_at desc", "IA_CAMPOS_LISTADOS", ct, new { DocumentoId = documentoId }).ConfigureAwait(false);

    [HttpPost("relatorios/gerar")]
    [HttpPost("relatorios/financeiro")]
    [HttpPost("relatorios/tributario")]
    [HttpPost("relatorios/comercial")]
    [HttpPost("relatorios/industria")]
    public async Task<ActionResult<ApiResponse<object>>> GerarRelatorio([FromBody] IaRelatorioRequest request, CancellationToken ct) => await Executar(new IaExecutionRequest("RELATORIO", request.Contexto ?? $"Gerar relatório {request.Tipo}", request.ModuloCodigo, "ASSISTENTE_GERAL", "RELATORIO", null), ct).ConfigureAwait(false);

    [HttpGet("automacoes")]
    public async Task<ActionResult<ApiResponse<object>>> Automacoes(CancellationToken ct) => await TenantQuery("select id, tenant_id, codigo, nome, descricao, modulo_codigo, gatilho, condicao_json, acao_json, exige_confirmacao, ativo, created_at, updated_at from sigov.ia_automacao where tenant_id=@TenantId order by created_at desc", "IA_AUTOMACOES_LISTADAS", ct).ConfigureAwait(false);
    [HttpPost("automacoes")]
    public async Task<ActionResult<ApiResponse<object>>> CriarAutomacao([FromBody] IaAutomationRequest request, CancellationToken ct) => await UpsertAutomacao(null, request, ct).ConfigureAwait(false);
    [HttpPut("automacoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarAutomacao(long id, [FromBody] IaAutomationRequest request, CancellationToken ct) => await UpsertAutomacao(id, request, ct).ConfigureAwait(false);
    [HttpPatch("automacoes/{id:long}/status")]
    public async Task<ActionResult<ApiResponse<object>>> StatusAutomacao(long id, [FromBody] Dictionary<string, bool> body, CancellationToken ct) => await Safe("IA_AUTOMACAO_ATUALIZADA", async (tenantId, cid) => { using var c = _context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition("update sigov.ia_automacao set ativo=@Ativo,updated_at=now() where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Id = id, Ativo = body.TryGetValue("ativo", out var ativo) && ativo }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id, correlationId = cid }, "Status atualizado.", cid)); }, ct).ConfigureAwait(false);
    [HttpPost("automacoes/{id:long}/executar")]
    public async Task<ActionResult<ApiResponse<object>>> ExecutarAutomacao(long id, CancellationToken ct) => await Safe("IA_AUTOMACAO_EXECUTADA", async (tenantId, cid) => Ok(ApiResponse<object>.Ok(new { execucaoId = await _automations.ExecutarAsync(tenantId, id, Guid.Parse(cid), ct).ConfigureAwait(false) }, correlationId: cid)), ct).ConfigureAwait(false);
    [HttpGet("automacoes/{id:long}/execucoes")]
    public async Task<ActionResult<ApiResponse<object>>> ExecucoesAutomacao(long id, CancellationToken ct) => await TenantQuery("select id, automacao_id, tenant_id, status, entrada_json, resultado_json, erro, correlation_id, created_at, concluida_at from sigov.ia_automacao_execucao where tenant_id=@TenantId and automacao_id=@AutomacaoId order by created_at desc", "IA_AUTOMACAO_EXECUCOES_LISTADAS", ct, new { AutomacaoId = id }).ConfigureAwait(false);

    [HttpGet("alertas")]
    public async Task<ActionResult<ApiResponse<object>>> Alertas(CancellationToken ct) => await TenantQuery("select id, tenant_id, modulo_codigo, tipo, titulo, mensagem, prioridade, origem, origem_id, lido, resolvido, created_at, resolvido_at from sigov.ia_alerta_inteligente where tenant_id=@TenantId order by created_at desc limit 100", "IA_ALERTAS_LISTADOS", ct).ConfigureAwait(false);
    [HttpPost("alertas/{id:long}/marcar-lido")]
    public async Task<ActionResult<ApiResponse<object>>> MarcarLido(long id, CancellationToken ct) => await FlagAlerta(id, "lido=true", "Alerta marcado como lido.", ct).ConfigureAwait(false);
    [HttpPost("alertas/{id:long}/resolver")]
    public async Task<ActionResult<ApiResponse<object>>> ResolverAlerta(long id, CancellationToken ct) => await FlagAlerta(id, "resolvido=true,resolvido_at=now()", "IA_ALERTA_RESOLVIDO", ct).ConfigureAwait(false);

    [HttpGet("predicoes")]
    public async Task<ActionResult<ApiResponse<object>>> Predicoes(CancellationToken ct) => await TenantQuery("select id, tenant_id, modelo_codigo, origem, origem_id, score, classificacao, explicacao, dados_json, created_at from sigov.ia_predicao_resultado where tenant_id=@TenantId order by created_at desc limit 100", "IA_PREDICOES_LISTADAS", ct).ConfigureAwait(false);
    [HttpPost("predicoes/inadimplencia")]
    [HttpPost("predicoes/estoque-ruptura")]
    [HttpPost("predicoes/os-atraso")]
    [HttpPost("predicoes/producao-atraso")]
    [HttpPost("predicoes/contrato-risco")]
    public async Task<ActionResult<ApiResponse<object>>> Predicao([FromBody] IaPredicaoRequest request, CancellationToken ct) => await Safe("IA_PREDICAO_EXECUTADA", async (tenantId, cid) => { var res = await _execution.ExecuteAsync(tenantId, UserId(), new IaExecutionRequest("PREDICAO", request.Contexto ?? "Predição baseada em regras", "ia_predicoes", "ASSISTENTE_GERAL", request.Origem, request.OrigemId), Guid.Parse(cid), ct).ConfigureAwait(false); using var c = _context.CreateConnection(); var id = await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.ia_predicao_resultado(tenant_id,modelo_codigo,origem,origem_id,score,classificacao,explicacao,dados_json) values(@TenantId,'predicao_api',@Origem,@OrigemId,0.75,'MEDIO',@Explicacao,'{}') returning id", new { TenantId = tenantId, request.Origem, request.OrigemId, Explicacao = res.Resposta }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id, res.ExecucaoId }, correlationId: cid)); }, ct).ConfigureAwait(false);

    [HttpPost("feedback")]
    public async Task<ActionResult<ApiResponse<object>>> Feedback([FromBody] IaFeedbackRequest request, CancellationToken ct) => await Safe("IA_FEEDBACK_REGISTRADO", async (tenantId, cid) => { using var c = _context.CreateConnection(); var id = await c.ExecuteScalarAsync<long>(new CommandDefinition("insert into sigov.ia_feedback_usuario(tenant_id,execucao_id,sugestao_id,usuario_id,avaliacao,comentario,util) values(@TenantId,@ExecucaoId,@SugestaoId,@UsuarioId,@Avaliacao,@Comentario,@Util) returning id", new { TenantId = tenantId, request.ExecucaoId, request.SugestaoId, UsuarioId = UserId(), request.Avaliacao, request.Comentario, request.Util }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }, ct).ConfigureAwait(false);

    [HttpGet("consumo")]
    public async Task<ActionResult<ApiResponse<object>>> Consumo(CancellationToken ct) => await TenantQuery("select id, tenant_id, competencia, interacoes, tokens_entrada, tokens_saida, custo_estimado, created_at from sigov.ia_consumo where tenant_id=@TenantId order by competencia desc limit 24", "IA_CONSUMO_VISUALIZADO", ct).ConfigureAwait(false);
    [HttpPost("consumo/recalcular")]
    public async Task<ActionResult<ApiResponse<object>>> Recalcular(CancellationToken ct) => await Safe("IA_CONSUMO_RECALCULADO", async (tenantId, cid) => { using var c = _context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition("insert into sigov.ia_consumo(tenant_id,competencia,interacoes,tokens_entrada,tokens_saida,custo_estimado) select tenant_id,date_trunc('month',created_at)::date,count(*),coalesce(sum(tokens_entrada),0),coalesce(sum(tokens_saida),0),coalesce(sum(custo_estimado),0) from sigov.ia_execucao where tenant_id=@TenantId group by tenant_id,date_trunc('month',created_at)::date on conflict(tenant_id,competencia) do update set interacoes=excluded.interacoes,tokens_entrada=excluded.tokens_entrada,tokens_saida=excluded.tokens_saida,custo_estimado=excluded.custo_estimado", new { TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { recalculado = true }, correlationId: cid)); }, ct).ConfigureAwait(false);

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard(CancellationToken ct) => await Safe("IA_DASHBOARD_VISUALIZADO", async (tenantId, cid) => { using var c = _context.CreateConnection(); var row = await c.QuerySingleAsync<object>(new CommandDefinition(@"select
coalesce((select interacoes from sigov.ia_consumo where tenant_id=@TenantId and competencia=date_trunc('month', now())::date),0) as interacoes_mes,
(select count(*) from sigov.ia_sugestao where tenant_id=@TenantId and status='PENDENTE') as sugestoes_pendentes,
(select count(*) from sigov.ia_alerta_inteligente where tenant_id=@TenantId and resolvido=false) as alertas_inteligentes,
(select count(*) from sigov.ia_automacao where tenant_id=@TenantId and ativo=true) as automacoes_ativas,
(select count(*) from sigov.ia_classificacao_documento where tenant_id=@TenantId) as documentos_classificados,
(select count(*) from sigov.ia_execucao where tenant_id=@TenantId and tipo='RELATORIO') as relatorios_gerados,
coalesce((select custo_estimado from sigov.ia_consumo where tenant_id=@TenantId and competencia=date_trunc('month', now())::date),0) as consumo_estimado,
(select limite_interacoes_mes from sigov.ia_configuracao_tenant where tenant_id=@TenantId) as limite_plano", new { TenantId = tenantId }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }, ct).ConfigureAwait(false);

    private async Task<ActionResult<ApiResponse<object>>> DocumentAction(long documentoId, IaExecutionRequest request, string audit, CancellationToken ct)
    {
        return await Safe(audit, async (tenantId, cid) => { var res = await _execution.ExecuteAsync(tenantId, UserId(), request, Guid.Parse(cid), ct).ConfigureAwait(false); using var c = _context.CreateConnection(); if (request.Tipo.Contains("CLASS", StringComparison.OrdinalIgnoreCase)) await c.ExecuteAsync(new CommandDefinition("insert into sigov.ia_classificacao_documento(tenant_id,documento_id,tipo_sugerido,confianca,metadados_json) values(@TenantId,@DocumentoId,'DOCUMENTO_GENERICO',0.75,jsonb_build_object('execucao_id',@ExecucaoId))", new { TenantId = tenantId, DocumentoId = documentoId, res.ExecucaoId }, cancellationToken: ct)).ConfigureAwait(false); else await c.ExecuteAsync(new CommandDefinition("insert into sigov.ia_extracao_campo(tenant_id,documento_id,campo,valor,confianca) values(@TenantId,@DocumentoId,'resumo',@Valor,0.75)", new { TenantId = tenantId, DocumentoId = documentoId, Valor = res.Resposta }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(res, correlationId: cid)); }, ct).ConfigureAwait(false);
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertAutomacao(long? id, IaAutomationRequest request, CancellationToken ct) => await Safe(id.HasValue ? "IA_AUTOMACAO_ATUALIZADA" : "IA_AUTOMACAO_CRIADA", async (tenantId, cid) => { using var c = _context.CreateConnection(); var result = await c.ExecuteScalarAsync<long>(new CommandDefinition(id.HasValue ? "update sigov.ia_automacao set codigo=@Codigo,nome=@Nome,descricao=@Descricao,modulo_codigo=@ModuloCodigo,gatilho=@Gatilho,condicao_json=cast(@CondicaoJson as jsonb),acao_json=cast(@AcaoJson as jsonb),exige_confirmacao=@ExigeConfirmacao,ativo=@Ativo,updated_at=now() where tenant_id=@TenantId and id=@Id returning id" : "insert into sigov.ia_automacao(tenant_id,codigo,nome,descricao,modulo_codigo,gatilho,condicao_json,acao_json,exige_confirmacao,ativo) values(@TenantId,@Codigo,@Nome,@Descricao,@ModuloCodigo,@Gatilho,cast(@CondicaoJson as jsonb),cast(@AcaoJson as jsonb),@ExigeConfirmacao,@Ativo) returning id", new { Id = id, TenantId = tenantId, request.Codigo, request.Nome, request.Descricao, request.ModuloCodigo, request.Gatilho, CondicaoJson = request.CondicaoJson ?? "{}", request.AcaoJson, request.ExigeConfirmacao, request.Ativo }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id = result }, correlationId: cid)); }, ct).ConfigureAwait(false);

    private async Task<ActionResult<ApiResponse<object>>> Query(string sql, string operation, CancellationToken ct) => await Safe(operation, async (_, cid) => { using var c = _context.CreateConnection(); return Ok(ApiResponse<object>.Ok(await c.QueryAsync<object>(new CommandDefinition(sql, cancellationToken: ct)).ConfigureAwait(false), correlationId: cid)); }, ct).ConfigureAwait(false);
    private async Task<ActionResult<ApiResponse<object>>> TenantQuery(string sql, string operation, CancellationToken ct, object? extra = null) => await Safe(operation, async (tenantId, cid) => { using var c = _context.CreateConnection(); var p = new DynamicParameters(extra); p.Add("TenantId", tenantId); return Ok(ApiResponse<object>.Ok(await c.QueryAsync<object>(new CommandDefinition(sql, p, cancellationToken: ct)).ConfigureAwait(false), correlationId: cid)); }, ct).ConfigureAwait(false);
    private async Task<ActionResult<ApiResponse<object>>> TenantSingle(string sql, object extra, string notFound, CancellationToken ct) => await Safe("IA_REGISTRO_OBTIDO", async (tenantId, cid) => { using var c = _context.CreateConnection(); var p = new DynamicParameters(extra); p.Add("TenantId", tenantId); var row = await c.QuerySingleOrDefaultAsync<object>(new CommandDefinition(sql, p, cancellationToken: ct)).ConfigureAwait(false); return row is null ? NotFound(ApiResponse<object>.Fail(notFound, cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }, ct).ConfigureAwait(false);
    private async Task<ActionResult<ApiResponse<object>>> UpdateStatus(string table, long id, string status, string message, CancellationToken ct) => await Safe("IA_STATUS_ATUALIZADO", async (tenantId, cid) => { using var c = _context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition($"update sigov.{table} set status=@Status where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Id = id, Status = status }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id, status }, message, cid)); }, ct).ConfigureAwait(false);
    private async Task<ActionResult<ApiResponse<object>>> SuggestionStatus(long id, string status, string setExtra, string audit, CancellationToken ct) => await Safe(audit, async (tenantId, cid) => { using var c = _context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition($"update sigov.ia_sugestao set status=@Status,{setExtra},usuario_decisao_id=@UsuarioId where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Id = id, Status = status, UsuarioId = UserId() }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id, status }, correlationId: cid)); }, ct).ConfigureAwait(false);
    private async Task<ActionResult<ApiResponse<object>>> FlagAlerta(long id, string set, string message, CancellationToken ct) => await Safe(message.StartsWith("IA_", StringComparison.Ordinal) ? message : "IA_ALERTA_ATUALIZADO", async (tenantId, cid) => { using var c = _context.CreateConnection(); await c.ExecuteAsync(new CommandDefinition($"update sigov.ia_alerta_inteligente set {set} where tenant_id=@TenantId and id=@Id", new { TenantId = tenantId, Id = id }, cancellationToken: ct)).ConfigureAwait(false); return Ok(ApiResponse<object>.Ok(new { id }, message, cid)); }, ct).ConfigureAwait(false);

    private async Task<ActionResult<ApiResponse<object>>> Safe(string operation, Func<long, string, Task<ActionResult<ApiResponse<object>>>> action, CancellationToken ct)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            return await action(tenantId, cid).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Regra de IA bloqueou operação {Operation}. CorrelationId={CorrelationId}", operation, cid);
            return StatusCode(403, ApiResponse<object>.Fail(ex.Message, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na operação de IA {Operation}. CorrelationId={CorrelationId}", operation, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao executar operação de IA. Tente novamente ou acione o suporte com o correlationId.", cid));
        }
    }

    private long RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório para operação de IA.");
    private long? UserId() => _user.UsuarioId ?? (long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null);
    private string CorrelationId() => HttpContext.Items.TryGetValue("CorrelationId", out var value) ? value?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
    private static async Task Audit(System.Data.IDbConnection c, long tenantId, string evento, string? entidade, long? registroId, object? payload, string cid, CancellationToken ct)
    {
        await c.ExecuteAsync(new CommandDefinition("insert into sigov.auditoria_evento(tenant_id,acao,entidade,entidade_id,depois,correlation_id) values(@TenantId,@Evento,coalesce(@Entidade,'ia'),@RegistroId,cast(@Payload as jsonb),cast(@CorrelationId as uuid))", new { TenantId = tenantId, Evento = evento, Entidade = entidade, RegistroId = registroId?.ToString(), Payload = System.Text.Json.JsonSerializer.Serialize(payload ?? new { }), CorrelationId = cid }, cancellationToken: ct)).ConfigureAwait(false);
    }
}
