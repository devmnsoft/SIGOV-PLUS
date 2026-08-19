using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/ged")]
[RequireModule("ged")]
public sealed class GedController : ControllerBase
{
    private readonly DapperContext _context;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly ILogger<GedController> _logger;

    public GedController(DapperContext context, ICurrentTenant tenant, ICurrentUser user, ILogger<GedController> logger)
    {
        _context = context;
        _tenant = tenant;
        _user = user;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard()
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission("ged.dashboard.visualizar", "ged.visualizar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var cards = await c.QuerySingleAsync<object>(@"select
(select count(*) from sigov.ged_documento where tenant_id=@TenantId and ativo=true and is_deleted=false) as documentos,
(select count(*) from sigov.ged_anexo where tenant_id=@TenantId and ativo=true and is_deleted=false) as anexos,
(select count(*) from sigov.ocr_digitalizacao where tenant_id=@TenantId and status='PROCESSADO') as ocr_processados,
(select count(*) from sigov.ged_assinatura where tenant_id=@TenantId and status='PENDENTE') as assinaturas_pendentes,
(select count(*) from sigov.contrato where tenant_id=@TenantId and status in ('VIGENTE','ASSINADO')) as contratos_ativos,
(select count(*) from sigov.fluxo_tramitacao where tenant_id=@TenantId and recebido_at is null) as tramitacoes_abertas", new { TenantId = tenantId });
            var porStatus = await c.QueryAsync<object>("select status, count(*) quantidade from sigov.ged_documento where tenant_id=@TenantId group by status order by status", new { TenantId = tenantId });
            await Auditar(c, tenantId, "GED_DASHBOARD_VISUALIZADO", null, null, new { tenantId }, cid);
            return Ok(ApiResponse<object>.Ok(new { cards, porStatus, versao = "Pós-Build 09" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no dashboard GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao carregar dashboard GED.", cid));
        }
    }

    [HttpGet("documentos")]
    public async Task<ActionResult<ApiResponse<object>>> ListarDocumentos([FromQuery] string? busca = null, [FromQuery] string? tipo = null, [FromQuery] string? status = null, [FromQuery] DateTime? dataInicio = null, [FromQuery] DateTime? dataFim = null, [FromQuery] string? indiceChave = null, [FromQuery] string? indiceValor = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission("ged.documento.visualizar", "ged.visualizar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>(@"select distinct d.id, d.tenant_id, d.entidade_id, d.exercicio_id,
d.tipo_documento_id, d.protocolo_id, d.contrato_id, d.origem_modulo, d.origem_entidade,
d.origem_id, d.titulo, d.descricao, d.numero_documento, d.tipo, d.status,
d.classificacao_lgpd, d.sigiloso, d.metadados, d.tags, d.data_documento, d.publicado_at,
d.ativo, d.created_at, d.created_by, d.updated_at, d.updated_by, d.correlation_id
from sigov.ged_documento d
left join sigov.ged_indice i on i.documento_id=d.id and i.tenant_id=d.tenant_id and i.is_deleted=false
where d.tenant_id=@TenantId and d.is_deleted=false
  and (@Busca is null or d.titulo ilike '%'||@Busca||'%' or d.descricao ilike '%'||@Busca||'%' or d.metadados::text ilike '%'||@Busca||'%')
  and (@Tipo is null or d.tipo=@Tipo)
  and (@Status is null or d.status=@Status)
  and (@DataInicio is null or d.data_documento>=@DataInicio)
  and (@DataFim is null or d.data_documento<=@DataFim)
  and (@IndiceChave is null or (i.chave=@IndiceChave and (@IndiceValor is null or i.valor ilike '%'||@IndiceValor||'%')))
order by d.created_at desc
offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Tipo = tipo, Status = status, DataInicio = dataInicio, DataFim = dataFim, IndiceChave = indiceChave, IndiceValor = indiceValor, Offset = Offset(page, pageSize), Limit = Limit(pageSize) });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar documentos.", cid));
        }
    }

    [HttpGet("documentos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> ObterDocumento(long id) => Obter("sigov.ged_documento", id, "ged.documento.visualizar", "ged.visualizar");

    [HttpPost("documentos")]
    public async Task<ActionResult<ApiResponse<object>>> CriarDocumento([FromBody] GedDocumentoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission("ged.documento.criar", "ged.upload")) return Forbid();
            if (string.IsNullOrWhiteSpace(request.Titulo)) return BadRequest(ApiResponse<object>.Fail("Título do documento é obrigatório.", cid));
            if (string.IsNullOrWhiteSpace(request.Tipo)) return BadRequest(ApiResponse<object>.Fail("Tipo do documento é obrigatório.", cid));
            if (string.IsNullOrWhiteSpace(request.OrigemModulo)) return BadRequest(ApiResponse<object>.Fail("Origem do documento é obrigatória.", cid));
            if (request.Sigiloso && string.IsNullOrWhiteSpace(request.JustificativaSigilo)) return BadRequest(ApiResponse<object>.Fail("Justificativa do sigilo é obrigatória.", cid));
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.ged_documento(tenant_id,entidade_id,exercicio_id,titulo,descricao,tipo,status,classificacao_lgpd,sigiloso,metadados,tags,origem_modulo,origem_entidade,origem_id,contrato_id,data_documento,created_by,correlation_id)
values(@TenantId,@EntidadeId,@ExercicioId,@Titulo,@Descricao,@Tipo,@Status,@ClassificacaoLgpd,@Sigiloso,cast(@Metadados as jsonb),@Tags,@OrigemModulo,@OrigemEntidade,@OrigemId,@ContratoId,@DataDocumento,@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, _tenant.EntidadeId, _tenant.ExercicioId, request.Titulo, request.Descricao, Tipo = request.Tipo ?? "GERAL", Status = request.Status ?? "RECEBIDO", ClassificacaoLgpd = request.ClassificacaoLgpd ?? "DADO_CONTROLADO", request.Sigiloso, Metadados = request.Metadados ?? "{}", Tags = request.Tags ?? Array.Empty<string>(), request.OrigemModulo, request.OrigemEntidade, request.OrigemId, request.ContratoId, request.DataDocumento, UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await Historico(c, tenantId, id, null, request.ContratoId, "GED_DOCUMENTO_CRIADO", "Documento criado no GED.", request, cid);
            await Auditar(c, tenantId, "GED_DOCUMENTO_CRIADO", "ged_documento", id, request, cid);
            return Created($"/api/ged/documentos/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar documento GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar documento GED.", cid));
        }
    }

    [HttpPost("documentos/{id:long}/anexos")]
    public async Task<ActionResult<ApiResponse<object>>> UploadAnexo(long id, IFormFile? arquivo, [FromForm] string? nomeArquivo = null)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission("processos.documento.anexar", "ged.documento.versionar", "ged.upload")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.ged_documento", tenantId, id)) return NotFound(ApiResponse<object>.Fail("Documento não encontrado.", cid));
            var fileName = arquivo?.FileName ?? nomeArquivo ?? $"documento-{id}.txt";
            var contentType = arquivo?.ContentType ?? "application/octet-stream";
            var tamanho = arquivo?.Length ?? 0;
            var hash = arquivo is null ? Sha256(fileName) : await HashAsync(arquivo);
            var anexoId = await c.ExecuteScalarAsync<long>(@"insert into sigov.ged_anexo(tenant_id,documento_id,nome_arquivo,content_type,tamanho_bytes,hash_sha256,storage_key,principal,created_by,correlation_id)
values(@TenantId,@DocumentoId,@NomeArquivo,@ContentType,@Tamanho,@Hash,@StorageKey,true,@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, DocumentoId = id, NomeArquivo = fileName, ContentType = contentType, Tamanho = tamanho, Hash = hash, StorageKey = $"tenant/{tenantId}/ged/{id}/{hash}", UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await Historico(c, tenantId, id, null, null, "GED_ANEXO_UPLOAD", $"Anexo {fileName} enviado.", new { anexoId, fileName, tamanho }, cid);
            await Auditar(c, tenantId, "GED_ANEXO_UPLOAD", "ged_anexo", anexoId, new { id, fileName, tamanho }, cid);
            return Ok(ApiResponse<object>.Ok(new { id = anexoId, documentoId = id, fileName, hash }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no upload GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao enviar anexo.", cid));
        }
    }

    [HttpGet("documentos/{id:long}/download")]
    public async Task<IActionResult> Download(long id)
    {
        if (!HasPermission("ged.download")) return Forbid();
        var tenantId = RequireTenant();
        using var c = _context.CreateConnection();
        var row = await c.QuerySingleOrDefaultAsync<(string Titulo, string? Hash)>(@"select d.titulo, a.hash_sha256 as hash from sigov.ged_documento d left join sigov.ged_anexo a on a.documento_id=d.id and a.principal=true where d.id=@Id and d.tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
        if (string.IsNullOrWhiteSpace(row.Titulo)) return NotFound();
        await Auditar(c, tenantId, "GED_DOCUMENTO_DOWNLOAD", "ged_documento", id, new { id }, CorrelationId());
        var bytes = Encoding.UTF8.GetBytes($"Download simulado SIGOV GED\nDocumento: {row.Titulo}\nHash: {row.Hash}\nTenant: {tenantId}\n");
        return File(bytes, "text/plain", $"ged-documento-{id}.txt");
    }

    [HttpPost("documentos/{id:long}/ocr")]
    public async Task<ActionResult<ApiResponse<object>>> ProcessarOcr(long id, [FromBody] OcrRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("ocr.processar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await Existe(c, "sigov.ged_documento", tenantId, id)) return NotFound(ApiResponse<object>.Fail("Documento não encontrado.", cid));
            var texto = request.TextoExtraido ?? $"OCR simulado para documento {id} em {DateTimeOffset.UtcNow:O}.";
            var ocrId = await c.ExecuteScalarAsync<long>(@"insert into sigov.ocr_digitalizacao(tenant_id,documento_id,anexo_id,status,motor,idioma,texto_extraido,metadados_extraidos,confianca_media,iniciado_at,concluido_at,created_by,correlation_id)
values(@TenantId,@DocumentoId,@AnexoId,'PROCESSADO','SIMULADO',@Idioma,@Texto,cast(@Metadados as jsonb),@Confianca,now(),now(),@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, DocumentoId = id, request.AnexoId, Idioma = request.Idioma ?? "pt-BR", Texto = texto, Metadados = request.MetadadosExtraidos ?? "{}", Confianca = request.ConfiancaMedia ?? 98.5m, UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await c.ExecuteAsync("update sigov.ged_documento set status='INDEXADO', updated_at=now(), updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, UsuarioId = _user.UsuarioId });
            await c.ExecuteAsync("insert into sigov.ged_indice(tenant_id,documento_id,chave,valor,tipo_valor,origem,confianca,created_by,correlation_id) values(@TenantId,@DocumentoId,'texto_ocr',@Texto,'TEXTO','OCR',@Confianca,@UsuarioId,cast(@CorrelationId as uuid))", new { TenantId = tenantId, DocumentoId = id, Texto = texto.Length > 500 ? texto[..500] : texto, Confianca = request.ConfiancaMedia ?? 98.5m, UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await Historico(c, tenantId, id, null, null, "GED_OCR_PROCESSADO", "OCR simulado processado e indexado.", new { ocrId, request.AnexoId }, cid);
            await Auditar(c, tenantId, "GED_OCR_PROCESSADO", "ocr_digitalizacao", ocrId, request, cid);
            return Ok(ApiResponse<object>.Ok(new { id = ocrId, documentoId = id, status = "PROCESSADO", textoExtraido = texto }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no OCR GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao processar OCR.", cid));
        }
    }

    [HttpPost("documentos/{id:long}/indices")]
    public Task<ActionResult<ApiResponse<object>>> Indexar(long id, [FromBody] GedIndiceRequest request) => CriarIndice(id, request);

    [HttpPost("documentos/{id:long}/assinaturas/simular")]
    public async Task<ActionResult<ApiResponse<object>>> Assinar(long id, [FromBody] AssinaturaRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("ged.assinar") && !HasPermission("contrato.assinar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var contratoId = await c.ExecuteScalarAsync<long?>("select contrato_id from sigov.ged_documento where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            var hash = Sha256($"{tenantId}:{id}:{request.SignatarioNome}:{DateTimeOffset.UtcNow:O}");
            var assinaturaId = await c.ExecuteScalarAsync<long>(@"insert into sigov.ged_assinatura(tenant_id,documento_id,contrato_id,usuario_id,signatario_nome,signatario_documento,tipo,status,hash_assinatura,evidencias,assinado_at,created_by,correlation_id)
values(@TenantId,@DocumentoId,@ContratoId,@UsuarioId,@Nome,@Documento,'SIMULADA','ASSINADO',@Hash,cast(@Evidencias as jsonb),now(),@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, DocumentoId = id, ContratoId = contratoId, UsuarioId = _user.UsuarioId, Nome = request.SignatarioNome, Documento = request.SignatarioDocumento, Hash = hash, Evidencias = JsonSerializer.Serialize(new { ip = HttpContext.Connection.RemoteIpAddress?.ToString(), userAgent = Request.Headers["User-Agent"].ToString(), aceite = request.AceiteLegal, modo = "SIMULADO" }), CorrelationId = GuidOrNew(cid) });
            await c.ExecuteAsync("update sigov.ged_documento set status='ASSINADO', updated_at=now(), updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, UsuarioId = _user.UsuarioId });
            if (contratoId.HasValue) await c.ExecuteAsync("update sigov.contrato set status='ASSINADO', updated_at=now(), updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId", new { Id = contratoId.Value, TenantId = tenantId, UsuarioId = _user.UsuarioId });
            await Historico(c, tenantId, id, null, contratoId, "GED_ASSINATURA_SIMULADA", "Assinatura digital simulada concluída.", new { assinaturaId, hash }, cid);
            await Auditar(c, tenantId, "GED_ASSINATURA_SIMULADA", "ged_assinatura", assinaturaId, new { id, contratoId, hash }, cid);
            return Ok(ApiResponse<object>.Ok(new { id = assinaturaId, documentoId = id, status = "ASSINADO", hash }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na assinatura simulada. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao assinar documento.", cid));
        }
    }

    [HttpPost("documentos/{id:long}/tramitar")]
    public async Task<ActionResult<ApiResponse<object>>> Tramitar(long id, [FromBody] TramitacaoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission("processos.processo.tramitar", "ged.tramitar")) return Forbid();
            if (!request.UnidadeDestinoId.HasValue && !request.UsuarioDestinoId.HasValue)
                return BadRequest(ApiResponse<object>.Fail("Unidade ou usuário de destino é obrigatório.", cid));
            if (string.IsNullOrWhiteSpace(request.Despacho))
                return BadRequest(ApiResponse<object>.Fail("Despacho é obrigatório.", cid));
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var statusAnterior = await c.ExecuteScalarAsync<string?>("select status from sigov.ged_documento where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            if (statusAnterior is null) return NotFound(ApiResponse<object>.Fail("Documento não encontrado.", cid));
            var fluxoId = await c.ExecuteScalarAsync<long>(@"insert into sigov.fluxo_tramitacao(tenant_id,documento_id,workflow_id,unidade_origem_id,unidade_destino_id,usuario_origem_id,usuario_destino_id,despacho,status_anterior,status_novo,prazo_at,created_by,correlation_id)
values(@TenantId,@DocumentoId,@WorkflowId,@UnidadeOrigemId,@UnidadeDestinoId,@UsuarioOrigemId,@UsuarioDestinoId,@Despacho,@StatusAnterior,@StatusNovo,@PrazoAt,@UsuarioOrigemId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, DocumentoId = id, request.WorkflowId, request.UnidadeOrigemId, request.UnidadeDestinoId, UsuarioOrigemId = _user.UsuarioId, request.UsuarioDestinoId, request.Despacho, StatusAnterior = statusAnterior, StatusNovo = request.StatusNovo ?? "EM_WORKFLOW", request.PrazoAt, CorrelationId = GuidOrNew(cid) });
            await c.ExecuteAsync("update sigov.ged_documento set status=@Status, updated_at=now(), updated_by=@UsuarioId where id=@Id and tenant_id=@TenantId", new { Status = request.StatusNovo ?? "EM_WORKFLOW", Id = id, TenantId = tenantId, UsuarioId = _user.UsuarioId });
            await Historico(c, tenantId, id, null, null, "GED_DOCUMENTO_TRAMITADO", request.Despacho, new { fluxoId, request.UnidadeDestinoId, request.UsuarioDestinoId }, cid);
            await Auditar(c, tenantId, "GED_DOCUMENTO_TRAMITADO", "fluxo_tramitacao", fluxoId, request, cid);
            return Ok(ApiResponse<object>.Ok(new { id = fluxoId, documentoId = id, status = request.StatusNovo ?? "EM_WORKFLOW" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao tramitar GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao tramitar documento.", cid));
        }
    }

    [HttpGet("documentos/{id:long}/historico")]
    public async Task<ActionResult<ApiResponse<object>>> HistoricoDocumento(long id)
    {
        var cid = CorrelationId();
        if (!HasAnyPermission("ged.documento.visualizar", "ged.visualizar")) return Forbid();
        var tenantId = RequireTenant();
        using var c = _context.CreateConnection();
        var rows = await c.QueryAsync<object>("select id, tenant_id, documento_id, protocolo_id, contrato_id, acao, descricao, usuario_id, antes, depois, ip, user_agent, evento_at, ativo, is_deleted, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, correlation_id from sigov.ged_historico where tenant_id=@TenantId and documento_id=@Id order by evento_at desc", new { TenantId = tenantId, Id = id });
        return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
    }

    [HttpGet("contratos")]
    public Task<ActionResult<ApiResponse<object>>> Contratos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.contrato", busca, page, pageSize, "created_at desc", "contrato.visualizar");

    [HttpPost("contratos")]
    public async Task<ActionResult<ApiResponse<object>>> CriarContrato([FromBody] ContratoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("contrato.criar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var contratoId = await c.ExecuteScalarAsync<long>(@"insert into sigov.contrato(tenant_id,entidade_id,exercicio_id,numero,objeto,contratado_nome,contratado_documento,origem_modulo,origem_id,valor_total,data_inicio,data_fim,status,metadados,created_by,correlation_id)
values(@TenantId,@EntidadeId,@ExercicioId,@Numero,@Objeto,@ContratadoNome,@ContratadoDocumento,@OrigemModulo,@OrigemId,@ValorTotal,@DataInicio,@DataFim,@Status,cast(@Metadados as jsonb),@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, _tenant.EntidadeId, _tenant.ExercicioId, request.Numero, request.Objeto, request.ContratadoNome, request.ContratadoDocumento, request.OrigemModulo, request.OrigemId, request.ValorTotal, request.DataInicio, request.DataFim, Status = request.Status ?? "RASCUNHO", Metadados = request.Metadados ?? "{}", UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            var docId = await c.ExecuteScalarAsync<long>(@"insert into sigov.ged_documento(tenant_id,entidade_id,exercicio_id,contrato_id,origem_modulo,origem_id,titulo,tipo,status,classificacao_lgpd,metadados,created_by,correlation_id)
values(@TenantId,@EntidadeId,@ExercicioId,@ContratoId,'contrato',@ContratoId,@Titulo,'CONTRATO','AGUARDANDO_ASSINATURA','DADO_PESSOAL',cast(@Metadados as jsonb),@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, _tenant.EntidadeId, _tenant.ExercicioId, ContratoId = contratoId, Titulo = $"Contrato {request.Numero} - {request.ContratadoNome}", Metadados = request.Metadados ?? "{}", UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await Historico(c, tenantId, docId, null, contratoId, "CONTRATO_CRIADO", "Contrato criado e vinculado ao GED.", request, cid);
            await Auditar(c, tenantId, "CONTRATO_CRIADO", "contrato", contratoId, new { request, documentoId = docId }, cid);
            return Created($"/api/ged/contratos/{contratoId}", ApiResponse<object>.Ok(new { id = contratoId, documentoId = docId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar contrato. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar contrato.", cid));
        }
    }

    [HttpGet("protocolos")]
    public Task<ActionResult<ApiResponse<object>>> Protocolos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.protocolo", busca, page, pageSize, "aberto_at desc", "ged.visualizar");

    [HttpPost("protocolos")]
    public async Task<ActionResult<ApiResponse<object>>> CriarProtocolo([FromBody] ProtocoloGedRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("ged.tramitar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var protocoloId = await c.ExecuteScalarAsync<long>(@"insert into sigov.protocolo(tenant_id,entidade_id,exercicio_id,numero,assunto,interessado_nome,interessado_documento,canal,status,documento_id,contrato_id,metadados,created_by,correlation_id)
values(@TenantId,@EntidadeId,@ExercicioId,@Numero,@Assunto,@InteressadoNome,@InteressadoDocumento,@Canal,@Status,@DocumentoId,@ContratoId,cast(@Metadados as jsonb),@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, _tenant.EntidadeId, _tenant.ExercicioId, request.Numero, request.Assunto, request.InteressadoNome, request.InteressadoDocumento, Canal = request.Canal ?? "PORTAL", Status = request.Status ?? "ABERTO", request.DocumentoId, request.ContratoId, Metadados = request.Metadados ?? "{}", UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            if (request.DocumentoId.HasValue) await Historico(c, tenantId, request.DocumentoId.Value, protocoloId, request.ContratoId, "PROTOCOLO_CRIADO", "Protocolo eletrônico vinculado ao documento.", request, cid);
            await Auditar(c, tenantId, "PROTOCOLO_CRIADO", "protocolo", protocoloId, request, cid);
            return Created($"/api/ged/protocolos/{protocoloId}", ApiResponse<object>.Ok(new { id = protocoloId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar protocolo GED. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar protocolo.", cid));
        }
    }

    [HttpGet("workflows")]
    public Task<ActionResult<ApiResponse<object>>> Workflows([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => Listar("sigov.ged_workflow", busca, page, pageSize, "created_at desc", "fluxo.visualizar");

    private async Task<ActionResult<ApiResponse<object>>> CriarIndice(long documentoId, GedIndiceRequest request)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission("ged.indexar")) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var indiceId = await c.ExecuteScalarAsync<long>("insert into sigov.ged_indice(tenant_id,documento_id,chave,valor,tipo_valor,origem,confianca,created_by,correlation_id) values(@TenantId,@DocumentoId,@Chave,@Valor,@TipoValor,@Origem,@Confianca,@UsuarioId,cast(@CorrelationId as uuid)) returning id", new { TenantId = tenantId, DocumentoId = documentoId, request.Chave, request.Valor, TipoValor = request.TipoValor ?? "TEXTO", Origem = request.Origem ?? "MANUAL", request.Confianca, UsuarioId = _user.UsuarioId, CorrelationId = GuidOrNew(cid) });
            await Historico(c, tenantId, documentoId, null, null, "GED_INDICE_CRIADO", $"Índice {request.Chave} criado.", request, cid);
            await Auditar(c, tenantId, "GED_INDICE_CRIADO", "ged_indice", indiceId, request, cid);
            return Ok(ApiResponse<object>.Ok(new { id = indiceId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao indexar documento. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao indexar documento.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> Listar(string tabela, string? busca, int page, int pageSize, string order, string permissao)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasPermission(permissao)) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>($"select {Projection(tabela)} from {tabela} t where t.tenant_id=@TenantId and t.is_deleted=false and (@Busca is null or t::text ilike '%'||@Busca||'%') order by {order} offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = Offset(page, pageSize), Limit = Limit(pageSize) });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar {Tabela}. CorrelationId={CorrelationId}", tabela, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar registros.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> Obter(string tabela, long id, params string[] permissoes)
    {
        var cid = CorrelationId();
        try
        {
            if (!HasAnyPermission(permissoes)) return Forbid();
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var row = await c.QuerySingleOrDefaultAsync<object>($"select {Projection(tabela)} from {tabela} where id=@Id and tenant_id=@TenantId and is_deleted=false", new { Id = id, TenantId = tenantId });
            return row is null ? NotFound(ApiResponse<object>.Fail("Registro não encontrado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter {Tabela}. CorrelationId={CorrelationId}", tabela, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter registro.", cid));
        }
    }

    private Task Historico(System.Data.IDbConnection c, long tenantId, long? documentoId, long? protocoloId, long? contratoId, string acao, string descricao, object payload, string cid) => c.ExecuteAsync(@"insert into sigov.ged_historico(tenant_id,documento_id,protocolo_id,contrato_id,acao,descricao,usuario_id,depois,ip,user_agent,correlation_id)
values(@TenantId,@DocumentoId,@ProtocoloId,@ContratoId,@Acao,@Descricao,@UsuarioId,cast(@Depois as jsonb),@Ip,@UserAgent,cast(@CorrelationId as uuid))", new { TenantId = tenantId, DocumentoId = documentoId, ProtocoloId = protocoloId, ContratoId = contratoId, Acao = acao, Descricao = descricao, UsuarioId = _user.UsuarioId, Depois = JsonSerializer.Serialize(payload), Ip = HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = Request.Headers["User-Agent"].ToString(), CorrelationId = GuidOrNew(cid) });

    private Task Auditar(System.Data.IDbConnection c, long tenantId, string evento, string? entidade, long? entityId, object payload, string cid) => c.ExecuteAsync("insert into sigov.auditoria_evento(tenant_id,usuario_id,acao,entidade,entidade_id,correlation_id,depois,created_at) values(@TenantId,@UsuarioId,@Evento,@Entidade,@RegistroId,cast(@CorrelationId as uuid),cast(@Payload as jsonb),now())", new { TenantId = tenantId, UsuarioId = _user.UsuarioId, Evento = evento, Entidade = entidade ?? "ged", RegistroId = entityId?.ToString(), CorrelationId = GuidOrNew(cid), Payload = JsonSerializer.Serialize(payload) });

    private static Task<bool> Existe(System.Data.IDbConnection c, string tabela, long tenantId, long id) => c.ExecuteScalarAsync<bool>($"select exists(select 1 from {tabela} where id=@Id and tenant_id=@TenantId and is_deleted=false)", new { Id = id, TenantId = tenantId });

    private static string Projection(string table) => table switch
    {
        "sigov.contrato" => "id, tenant_id, numero, objeto, fornecedor_id, status, vigencia_inicio, vigencia_fim, valor, created_at, updated_at, is_deleted",
        "sigov.ged_documento" => "id, tenant_id, entidade_id, exercicio_id, tipo_documento_id, protocolo_id, contrato_id, origem_modulo, origem_entidade, origem_id, titulo, descricao, numero_documento, tipo, status, classificacao_lgpd, sigiloso, metadados, tags, data_documento, publicado_at, ativo, is_deleted, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, correlation_id",
        "sigov.ged_workflow" => "id, tenant_id, documento_id, codigo, nome, etapa_atual, status, responsavel_usuario_id, responsavel_perfil, definicao, iniciado_at, concluido_at, ativo, is_deleted, created_at, created_by, updated_at, updated_by, deleted_at, deleted_by, correlation_id",
        "sigov.protocolo" => "id, tenant_id, numero, assunto, interessado_nome, interessado_documento, status, workflow_instancia_id, correlation_id, created_at, updated_at, is_deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Tabela fora da allowlist de projeções.")
    };
    private long RequireTenant() => _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para GED.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
    private static int Limit(int pageSize) => Math.Clamp(pageSize, 1, 100);
    private static int Offset(int page, int pageSize) => (Math.Max(1, page) - 1) * Limit(pageSize);
    private bool HasPermission(string permission) => User.Identity?.IsAuthenticated != true || User.IsInRole("ADMIN_GERAL") || User.IsInRole("ADMIN_TENANT") || User.Claims.Any(c => (c.Type == "permission" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
    private bool HasAnyPermission(params string[] permissions) => permissions.Any(HasPermission);
    private static Guid GuidOrNew(string value) => Guid.TryParse(value, out var parsed) ? parsed : Guid.NewGuid();
    private static string Sha256(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static async Task<string> HashAsync(IFormFile file)
    {
        await using var source = file.OpenReadStream();
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(buffer.ToArray())).ToLowerInvariant();
    }
}

public sealed record GedDocumentoRequest(string Titulo, string? Descricao, string? Tipo, string? Status, string? ClassificacaoLgpd, bool Sigiloso, string? JustificativaSigilo, string? Metadados, string[]? Tags, string? OrigemModulo, string? OrigemEntidade, long? OrigemId, long? ContratoId, DateTime? DataDocumento);
public sealed record GedIndiceRequest(string Chave, string Valor, string? TipoValor, string? Origem, decimal? Confianca);
public sealed record OcrRequest(long? AnexoId, string? Idioma, string? TextoExtraido, string? MetadadosExtraidos, decimal? ConfiancaMedia);
public sealed record AssinaturaRequest(string SignatarioNome, string? SignatarioDocumento, bool AceiteLegal);
public sealed record TramitacaoRequest(long? WorkflowId, long? UnidadeOrigemId, long? UnidadeDestinoId, long? UsuarioDestinoId, string Despacho, string? StatusNovo, DateTimeOffset? PrazoAt);
public sealed record ContratoRequest(string Numero, string Objeto, string ContratadoNome, string? ContratadoDocumento, string? OrigemModulo, long? OrigemId, decimal ValorTotal, DateTime? DataInicio, DateTime? DataFim, string? Status, string? Metadados);
public sealed record ProtocoloGedRequest(string Numero, string Assunto, string? InteressadoNome, string? InteressadoDocumento, string? Canal, string? Status, long? DocumentoId, long? ContratoId, string? Metadados);
