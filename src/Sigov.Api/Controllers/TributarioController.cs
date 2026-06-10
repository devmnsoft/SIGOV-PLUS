using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;
using System.Text.Json;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/tributario")]
[RequireModule("tributario")]
public sealed class TributarioController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly DapperContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TributarioController> _logger;

    public TributarioController(IWebHostEnvironment environment, DapperContext context, ICurrentTenant currentTenant, ICurrentUser currentUser, ILogger<TributarioController> logger)
    {
        _environment = environment;
        _context = context;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard()
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var data = new
            {
                contribuintesAtivos = await Count(c, "sigov.tributario_contribuinte", tenantId),
                imoveisAtivos = await Count(c, "sigov.tributario_imovel", tenantId),
                economicosAtivos = await Count(c, "sigov.tributario_economico", tenantId),
                camposDinamicosAtivos = await Count(c, "sigov.tributario_campo_dinamico", tenantId),
                configuracaoConcluida = await c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.tributario_configuracao where tenant_id=@TenantId and (inscricao_imobiliaria_mascara is not null or inscricao_mobiliaria_mascara is not null))", new { TenantId = tenantId })
            };
            return Ok(ApiResponse<object>.Ok(data, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no dashboard tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao carregar dashboard tributário.", cid));
        }
    }

    [HttpGet("configuracao")]
    public async Task<ActionResult<ApiResponse<object>>> Configuracao()
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("insert into sigov.tributario_configuracao(tenant_id) values(@TenantId) on conflict(tenant_id) do nothing", new { TenantId = tenantId });
            var row = await c.QuerySingleAsync<object>("select * from sigov.tributario_configuracao where tenant_id=@TenantId", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(row, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter configuração tributária. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter configuração.", cid));
        }
    }

    [HttpPut("configuracao")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarConfiguracao([FromBody] TributarioConfiguracaoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync(@"insert into sigov.tributario_configuracao(tenant_id,inscricao_imobiliaria_mascara,inscricao_mobiliaria_mascara,usa_georreferenciamento,usa_integracao_nfse,usa_protesto,updated_at)
values(@TenantId,@InscricaoImobiliariaMascara,@InscricaoMobiliariaMascara,@UsaGeorreferenciamento,@UsaIntegracaoNfse,@UsaProtesto,now())
on conflict(tenant_id) do update set inscricao_imobiliaria_mascara=excluded.inscricao_imobiliaria_mascara, inscricao_mobiliaria_mascara=excluded.inscricao_mobiliaria_mascara, usa_georreferenciamento=excluded.usa_georreferenciamento, usa_integracao_nfse=excluded.usa_integracao_nfse, usa_protesto=excluded.usa_protesto, updated_at=now()", new { TenantId = tenantId, request.InscricaoImobiliariaMascara, request.InscricaoMobiliariaMascara, request.UsaGeorreferenciamento, request.UsaIntegracaoNfse, request.UsaProtesto });
            await Auditar(c, tenantId, "TRIBUTARIO_CONFIGURACAO_ATUALIZADA", request, cid);
            return Ok(ApiResponse<object>.Ok(new { tenantId }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar configuração tributária. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar configuração.", cid));
        }
    }

    [HttpGet("tipos-cadastro")]
    public Task<ActionResult<ApiResponse<object>>> Tipos() => Listar("sigov.tributario_tipo_cadastro", "codigo", null);
    [HttpPost("tipos-cadastro")]
    public Task<ActionResult<ApiResponse<object>>> CriarTipo([FromBody] TipoCadastroRequest r) => InserirTipo(r);
    [HttpPut("tipos-cadastro/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarTipo(long id, [FromBody] TipoCadastroRequest r) => AtualizarTipoCadastro(id, r);
    [HttpPatch("tipos-cadastro/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusTipo(long id, [FromBody] StatusAtivoRequest r) => Status("sigov.tributario_tipo_cadastro", id, r.Ativo, "TRIBUTARIO_TIPO_STATUS");

    [HttpGet("campos-dinamicos")]
    public Task<ActionResult<ApiResponse<object>>> Campos([FromQuery] string? tipoCadastroCodigo = null) => Listar("sigov.tributario_campo_dinamico", "ordem,codigo", tipoCadastroCodigo);
    [HttpPost("campos-dinamicos")]
    public Task<ActionResult<ApiResponse<object>>> CriarCampo([FromBody] CampoDinamicoRequest r) => InserirCampo(r);
    [HttpPut("campos-dinamicos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarCampo(long id, [FromBody] CampoDinamicoRequest r) => AtualizarCampoDinamico(id, r);
    [HttpPatch("campos-dinamicos/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusCampo(long id, [FromBody] StatusAtivoRequest r) => Status("sigov.tributario_campo_dinamico", id, r.Ativo, "TRIBUTARIO_CAMPO_STATUS");

    [HttpGet("contribuintes")]
    public Task<ActionResult<ApiResponse<object>>> Contribuintes([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarContribuintes(busca, page, pageSize);
    [HttpGet("contribuintes/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Contribuinte(long id) => ObterPorId("sigov.tributario_contribuinte", id);
    [HttpPost("contribuintes")]
    public Task<ActionResult<ApiResponse<object>>> CriarContribuinte([FromBody] ContribuinteRequest r) => UpsertContribuinte(null, r);
    [HttpPut("contribuintes/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarContribuinte(long id, [FromBody] ContribuinteRequest r) => UpsertContribuinte(id, r);

    [HttpGet("imoveis")]
    public Task<ActionResult<ApiResponse<object>>> Imoveis([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.tributario_imovel", busca, page, pageSize);
    [HttpGet("imoveis/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Imovel(long id) => ObterPorId("sigov.tributario_imovel", id);
    [HttpPost("imoveis")]
    public Task<ActionResult<ApiResponse<object>>> CriarImovel([FromBody] ImovelRequest r) => UpsertImovel(null, r);
    [HttpPut("imoveis/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarImovel(long id, [FromBody] ImovelRequest r) => UpsertImovel(id, r);

    [HttpGet("economicos")]
    public Task<ActionResult<ApiResponse<object>>> Economicos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastro("sigov.tributario_economico", busca, page, pageSize);
    [HttpGet("economicos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> Economico(long id) => ObterPorId("sigov.tributario_economico", id);
    [HttpPost("economicos")]
    public Task<ActionResult<ApiResponse<object>>> CriarEconomico([FromBody] EconomicoRequest r) => UpsertEconomico(null, r);
    [HttpPut("economicos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarEconomico(long id, [FromBody] EconomicoRequest r) => UpsertEconomico(id, r);

    [HttpPost("dam-dev")]
    public ActionResult<ApiResponse<object>> GerarDamDev([FromBody] DevIntegrationRequest request) => DevOnly("DAM fake", request);
    [HttpPost("pix-dev")]
    public ActionResult<ApiResponse<object>> GerarPixDev([FromBody] DevIntegrationRequest request) => DevOnly("PIX dev", request);
    [HttpPost("pagamentos-dev")]
    public ActionResult<ApiResponse<object>> RegistrarPagamentoDev([FromBody] DevIntegrationRequest request) => DevOnly("pagamento dev", request);

    private async Task<ActionResult<ApiResponse<object>>> Listar(string tabela, string order, string? tipo)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var sql = $"select * from {tabela} where tenant_id=@TenantId" + (tipo is null ? string.Empty : " and tipo_cadastro_codigo=@Tipo") + $" order by {order}";
            var rows = await c.QueryAsync<object>(sql, new { TenantId = tenantId, Tipo = tipo });
            return Ok(ApiResponse<object>.Ok(rows, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar dados tributários.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> InserirTipo(TipoCadastroRequest r)
    {
        var cid = CorrelationId();
        try
        {
            if (string.IsNullOrWhiteSpace(r.Codigo) || string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Código e nome são obrigatórios.", cid));
            var tenantId = RequireTenant(); using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.tributario_tipo_cadastro(tenant_id,codigo,nome,descricao,ativo) values(@TenantId,@Codigo,@Nome,@Descricao,true) returning id", new { TenantId = tenantId, Codigo = r.Codigo.Trim().ToUpperInvariant(), r.Nome, r.Descricao });
            await Auditar(c, tenantId, "TRIBUTARIO_TIPO_CRIADO", r, cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar tipo tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar tipo.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarTipoCadastro(long id, TipoCadastroRequest r)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync("update sigov.tributario_tipo_cadastro set nome=@Nome, descricao=@Descricao where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.Nome, r.Descricao }); await Auditar(c, tenantId, "TRIBUTARIO_TIPO_ATUALIZADO", new { id, r }, cid); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar tipo tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar tipo.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> InserirCampo(CampoDinamicoRequest r)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var id = await c.ExecuteScalarAsync<long>("insert into sigov.tributario_campo_dinamico(tenant_id,tipo_cadastro_codigo,codigo,nome,tipo,obrigatorio,ordem,opcoes_json,ativo) values(@TenantId,@TipoCadastroCodigo,@Codigo,@Nome,@Tipo,@Obrigatorio,@Ordem,cast(@OpcoesJson as jsonb),true) returning id", new { TenantId = tenantId, TipoCadastroCodigo = r.TipoCadastroCodigo.Trim().ToUpperInvariant(), Codigo = r.Codigo.Trim().ToUpperInvariant(), r.Nome, r.Tipo, r.Obrigatorio, r.Ordem, OpcoesJson = r.OpcoesJson ?? "null" }); await Auditar(c, tenantId, "TRIBUTARIO_CAMPO_CRIADO", r, cid); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao criar campo tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar campo.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarCampoDinamico(long id, CampoDinamicoRequest r)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync("update sigov.tributario_campo_dinamico set nome=@Nome,tipo=@Tipo,obrigatorio=@Obrigatorio,ordem=@Ordem,opcoes_json=cast(@OpcoesJson as jsonb) where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.Nome, r.Tipo, r.Obrigatorio, r.Ordem, OpcoesJson = r.OpcoesJson ?? "null" }); await Auditar(c, tenantId, "TRIBUTARIO_CAMPO_ATUALIZADO", new { id, r }, cid); return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao atualizar campo tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar campo.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarContribuintes(string? busca, int page, int pageSize)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var rows = await c.QueryAsync<object>(@"select id, tenant_id, nome, case when documento is null then null else concat('***', right(documento,4)) end as documento, case when email is null then null else concat(left(email,1),'***@', split_part(email,'@',2)) end as email, case when telefone is null then null else concat('***', right(telefone,4)) end as telefone, tipo_pessoa, ativo, created_at, updated_at from sigov.tributario_contribuinte where tenant_id=@TenantId and (@Busca is null or nome ilike '%'||@Busca||'%' or documento ilike '%'||@Busca||'%') order by nome offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = (Math.Max(1, page)-1)*Math.Clamp(pageSize,1,100), Limit = Math.Clamp(pageSize,1,100) }); return Ok(ApiResponse<object>.Ok(rows, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar contribuintes. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar contribuintes.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarCadastro(string tabela, string? busca, int page, int pageSize)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var filtroExtra = tabela.Contains("economico", StringComparison.Ordinal) ? " or coalesce(nome_fantasia,'') ilike '%'||@Busca||'%'" : string.Empty; var rows = await c.QueryAsync<object>($"select * from {tabela} where tenant_id=@TenantId and (@Busca is null or inscricao ilike '%'||@Busca||'%'{filtroExtra}) order by inscricao offset @Offset limit @Limit", new { TenantId = tenantId, Busca = busca, Offset = (Math.Max(1, page)-1)*Math.Clamp(pageSize,1,100), Limit = Math.Clamp(pageSize,1,100) }); return Ok(ApiResponse<object>.Ok(rows, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao listar cadastro tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar cadastro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterPorId(string tabela, long id)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var row = await c.QuerySingleOrDefaultAsync<object>($"select * from {tabela} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId }); return row is null ? NotFound(ApiResponse<object>.Fail("Registro não encontrado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao obter registro tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter registro.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertContribuinte(long? id, ContribuinteRequest r)
    {
        var cid = CorrelationId();
        try { if (string.IsNullOrWhiteSpace(r.Nome)) return BadRequest(ApiResponse<object>.Fail("Nome é obrigatório.", cid)); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var newId = id ?? await c.ExecuteScalarAsync<long>("insert into sigov.tributario_contribuinte(tenant_id,nome,documento,email,telefone,tipo_pessoa,dados_json) values(@TenantId,@Nome,@Documento,@Email,@Telefone,@TipoPessoa,cast(@DadosJson as jsonb)) returning id", new { TenantId = tenantId, r.Nome, r.Documento, r.Email, r.Telefone, r.TipoPessoa, DadosJson = r.DadosJson ?? "{}" }); if (id.HasValue) await c.ExecuteAsync("update sigov.tributario_contribuinte set nome=@Nome,documento=@Documento,email=@Email,telefone=@Telefone,tipo_pessoa=@TipoPessoa,dados_json=cast(@DadosJson as jsonb),updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id.Value, TenantId = tenantId, r.Nome, r.Documento, r.Email, r.Telefone, r.TipoPessoa, DadosJson = r.DadosJson ?? "{}" }); await Auditar(c, tenantId, id.HasValue ? "TRIBUTARIO_CONTRIBUINTE_ATUALIZADO" : "TRIBUTARIO_CONTRIBUINTE_CRIADO", r, cid); return Ok(ApiResponse<object>.Ok(new { id = id ?? newId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gravar contribuinte. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gravar contribuinte.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertImovel(long? id, ImovelRequest r)
    {
        var cid = CorrelationId();
        try { if (string.IsNullOrWhiteSpace(r.Inscricao)) return BadRequest(ApiResponse<object>.Fail("Inscrição é obrigatória.", cid)); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var newId = id ?? await c.ExecuteScalarAsync<long>("insert into sigov.tributario_imovel(tenant_id,inscricao,contribuinte_id,endereco_json,area_terreno,area_construida,dados_json) values(@TenantId,@Inscricao,@ContribuinteId,cast(@EnderecoJson as jsonb),@AreaTerreno,@AreaConstruida,cast(@DadosJson as jsonb)) returning id", new { TenantId = tenantId, r.Inscricao, r.ContribuinteId, EnderecoJson = r.EnderecoJson ?? "{}", r.AreaTerreno, r.AreaConstruida, DadosJson = r.DadosJson ?? "{}" }); if (id.HasValue) await c.ExecuteAsync("update sigov.tributario_imovel set inscricao=@Inscricao,contribuinte_id=@ContribuinteId,endereco_json=cast(@EnderecoJson as jsonb),area_terreno=@AreaTerreno,area_construida=@AreaConstruida,dados_json=cast(@DadosJson as jsonb),updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id.Value, TenantId = tenantId, r.Inscricao, r.ContribuinteId, EnderecoJson = r.EnderecoJson ?? "{}", r.AreaTerreno, r.AreaConstruida, DadosJson = r.DadosJson ?? "{}" }); await Auditar(c, tenantId, id.HasValue ? "TRIBUTARIO_IMOVEL_ATUALIZADO" : "TRIBUTARIO_IMOVEL_CRIADO", r, cid); return Ok(ApiResponse<object>.Ok(new { id = id ?? newId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gravar imóvel. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gravar imóvel.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> UpsertEconomico(long? id, EconomicoRequest r)
    {
        var cid = CorrelationId();
        try { if (string.IsNullOrWhiteSpace(r.Inscricao)) return BadRequest(ApiResponse<object>.Fail("Inscrição é obrigatória.", cid)); var tenantId = RequireTenant(); using var c = _context.CreateConnection(); var newId = id ?? await c.ExecuteScalarAsync<long>("insert into sigov.tributario_economico(tenant_id,inscricao,contribuinte_id,nome_fantasia,atividade_principal,dados_json) values(@TenantId,@Inscricao,@ContribuinteId,@NomeFantasia,@AtividadePrincipal,cast(@DadosJson as jsonb)) returning id", new { TenantId = tenantId, r.Inscricao, r.ContribuinteId, r.NomeFantasia, r.AtividadePrincipal, DadosJson = r.DadosJson ?? "{}" }); if (id.HasValue) await c.ExecuteAsync("update sigov.tributario_economico set inscricao=@Inscricao,contribuinte_id=@ContribuinteId,nome_fantasia=@NomeFantasia,atividade_principal=@AtividadePrincipal,dados_json=cast(@DadosJson as jsonb),updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id.Value, TenantId = tenantId, r.Inscricao, r.ContribuinteId, r.NomeFantasia, r.AtividadePrincipal, DadosJson = r.DadosJson ?? "{}" }); await Auditar(c, tenantId, id.HasValue ? "TRIBUTARIO_ECONOMICO_ATUALIZADO" : "TRIBUTARIO_ECONOMICO_CRIADO", r, cid); return Ok(ApiResponse<object>.Ok(new { id = id ?? newId }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao gravar econômico. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao gravar econômico.", cid)); }
    }

    private async Task<ActionResult<ApiResponse<object>>> Status(string tabela, long id, bool ativo, string evento)
    {
        var cid = CorrelationId();
        try { var tenantId = RequireTenant(); using var c = _context.CreateConnection(); await c.ExecuteAsync($"update {tabela} set ativo=@Ativo where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, Ativo = ativo }); await Auditar(c, tenantId, evento, new { id, ativo }, cid); return Ok(ApiResponse<object>.Ok(new { id, ativo }, correlationId: cid)); }
        catch (Exception ex) { _logger.LogError(ex, "Erro ao alterar status tributário. CorrelationId={CorrelationId}", cid); return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status.", cid)); }
    }

    private static Task<int> Count(System.Data.IDbConnection c, string table, long tenantId) => c.ExecuteScalarAsync<int>($"select count(*) from {table} where tenant_id=@TenantId and ativo=true", new { TenantId = tenantId });
    private long RequireTenant() => _currentTenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório para operar Tributário.");
    private string CorrelationId() => HttpContext.TraceIdentifier;
    private Task Auditar(System.Data.IDbConnection c, long tenantId, string tipo, object payload, string cid) => c.ExecuteAsync("insert into sigov.saas_evento_comercial(tenant_id,tipo_evento,descricao,origem,usuario_id,payload,correlation_id) values(@TenantId,@Tipo,@Tipo,'tributario',@UsuarioId,cast(@Payload as jsonb),@CorrelationId)", new { TenantId = tenantId, Tipo = tipo, UsuarioId = _currentUser.UsuarioId, Payload = JsonSerializer.Serialize(payload), CorrelationId = Guid.NewGuid() });

    private ActionResult<ApiResponse<object>> DevOnly(string recurso, DevIntegrationRequest request)
    {
        if (!_environment.IsDevelopment()) return StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Fail($"{recurso} disponível somente em Development. Integração real não configurada para este ambiente."));
        return Ok(ApiResponse<object>.Ok(new { request.ParcelaId, request.Valor, ambiente = _environment.EnvironmentName }));
    }
}

public sealed record TributarioConfiguracaoRequest(string? InscricaoImobiliariaMascara, string? InscricaoMobiliariaMascara, bool UsaGeorreferenciamento, bool UsaIntegracaoNfse, bool UsaProtesto);
public sealed record TipoCadastroRequest(string Codigo, string Nome, string? Descricao);
public sealed record CampoDinamicoRequest(string TipoCadastroCodigo, string Codigo, string Nome, string Tipo, bool Obrigatorio, int Ordem, string? OpcoesJson);
public sealed record StatusAtivoRequest(bool Ativo);
public sealed record ContribuinteRequest(string Nome, string? Documento, string? Email, string? Telefone, string? TipoPessoa, string? DadosJson);
public sealed record ImovelRequest(string Inscricao, long? ContribuinteId, string? EnderecoJson, decimal? AreaTerreno, decimal? AreaConstruida, string? DadosJson);
public sealed record EconomicoRequest(string Inscricao, long? ContribuinteId, string? NomeFantasia, string? AtividadePrincipal, string? DadosJson);
public sealed record DevIntegrationRequest(long ParcelaId, decimal? Valor);
