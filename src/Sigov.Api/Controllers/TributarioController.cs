using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/tributario")]
[RequireModule("tributario")]
public sealed class TributarioController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly IReadOnlyDictionary<string, TributarioResource> Resources = new Dictionary<string, TributarioResource>(StringComparer.OrdinalIgnoreCase)
    {
        ["contribuintes"] = new("sigov.contribuinte", "inscricao", "nome", "inscricao", "tributario.contribuinte"),
        ["iptu"] = new("sigov.iptu", "inscricao_imobiliaria", "data_vencimento", "inscricao_imobiliaria", "tributario.iptu"),
        ["iss"] = new("sigov.iss", "inscricao_municipal", "data_vencimento", "inscricao_municipal", "tributario.iss"),
        ["taxas"] = new("sigov.taxas_municipais", "inscricao", "data_vencimento", "inscricao", "tributario.taxas"),
        ["parcelas"] = new("sigov.parcela", "status", "data_vencimento", "origem_tipo", "tributario.parcela"),
        ["arrecadacoes"] = new("sigov.arrecadacao", "status", "data_pagamento desc", "codigo_baixa", "tributario.arrecadacao"),
        ["dam"] = new("sigov.documento_arrecadacao_municipal", "numero", "data_vencimento", "numero", "tributario.dam"),
        ["livro-eletronico"] = new("sigov.livro_eletronico_tributario", "tipo", "competencia desc", "tipo", "tributario.livro_eletronico"),
        ["parcelamentos"] = new("sigov.parcelamento_divida_ativa", "numero", "created_at desc", "inscricao_divida", "tributario.parcelamento"),
        ["nfse"] = new("sigov.integracao_nfse", "inscricao_municipal", "created_at desc", "rps_numero", "tributario.nfse")
    };

    private readonly IWebHostEnvironment _environment;
    private readonly DapperContext _context;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionService _permissions;
    private readonly ILogger<TributarioController> _logger;

    public TributarioController(
        IWebHostEnvironment environment,
        DapperContext context,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IPermissionService permissions,
        ILogger<TributarioController> logger)
    {
        _environment = environment;
        _context = context;
        _currentTenant = currentTenant;
        _currentUser = currentUser;
        _permissions = permissions;
        _logger = logger;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<object>>> Dashboard()
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            if (!await TemPermissao("dashboard", "visualizar")) return Forbid();
            using var c = _context.CreateConnection();
            var data = new
            {
                contribuintesAtivos = await Count(c, "sigov.contribuinte", tenantId, "ativo=true"),
                iptuAberto = await Count(c, "sigov.iptu", tenantId, "status='ABERTO'"),
                issAberto = await Count(c, "sigov.iss", tenantId, "status='ABERTO'"),
                taxasAbertas = await Count(c, "sigov.taxas_municipais", tenantId, "status='ABERTO'"),
                parcelasVencidas = await Count(c, "sigov.parcela", tenantId, "status='ABERTA' and data_vencimento < current_date"),
                damsEmitidos = await Count(c, "sigov.documento_arrecadacao_municipal", tenantId, "status='EMITIDO'"),
                nfseSimuladas = await Count(c, "sigov.integracao_nfse", tenantId, "status='SIMULADA'"),
                totalLancado = await ScalarDecimal(c, "select coalesce((select sum(valor_lancado) from sigov.iptu where tenant_id=@TenantId),0) + coalesce((select sum(valor_lancado) from sigov.iss where tenant_id=@TenantId),0) + coalesce((select sum(valor) from sigov.taxas_municipais where tenant_id=@TenantId),0)", tenantId),
                totalArrecadado = await ScalarDecimal(c, "select coalesce(sum(valor_pago),0) from sigov.arrecadacao where tenant_id=@TenantId and status='CONFIRMADA'", tenantId),
                versaoRelatorio = "Pós-Build 08"
            };
            await Auditar(c, tenantId, "TRIBUTARIO_DASHBOARD_VISUALIZADO", new { tenantId }, cid);
            return Ok(ApiResponse<object>.Ok(data, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no dashboard tributário avançado. CorrelationId={CorrelationId}", cid);
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
    public Task<ActionResult<ApiResponse<object>>> Tipos() => ListarLegado("sigov.tributario_tipo_cadastro", "codigo", null);

    [HttpPost("tipos-cadastro")]
    public Task<ActionResult<ApiResponse<object>>> CriarTipo([FromBody] TipoCadastroRequest r) => InserirTipo(r);

    [HttpPut("tipos-cadastro/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarTipo(long id, [FromBody] TipoCadastroRequest r) => AtualizarTipoCadastro(id, r);

    [HttpPatch("tipos-cadastro/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusTipo(long id, [FromBody] StatusAtivoRequest r) => Status("sigov.tributario_tipo_cadastro", id, r.Ativo, "TRIBUTARIO_TIPO_STATUS");

    [HttpGet("campos-dinamicos")]
    public Task<ActionResult<ApiResponse<object>>> Campos([FromQuery] string? tipoCadastroCodigo = null) => ListarLegado("sigov.tributario_campo_dinamico", "ordem,codigo", tipoCadastroCodigo);

    [HttpPost("campos-dinamicos")]
    public Task<ActionResult<ApiResponse<object>>> CriarCampo([FromBody] CampoDinamicoRequest r) => InserirCampo(r);

    [HttpPut("campos-dinamicos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarCampo(long id, [FromBody] CampoDinamicoRequest r) => AtualizarCampoDinamico(id, r);

    [HttpPatch("campos-dinamicos/{id:long}/status")]
    public Task<ActionResult<ApiResponse<object>>> StatusCampo(long id, [FromBody] StatusAtivoRequest r) => Status("sigov.tributario_campo_dinamico", id, r.Ativo, "TRIBUTARIO_CAMPO_STATUS");

    [HttpGet("contribuintes-base")]
    [HttpGet("contribuintes-legado")]
    public Task<ActionResult<ApiResponse<object>>> ContribuintesLegado([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarContribuintesLegado(busca, page, pageSize);

    [HttpGet("imoveis")]
    public Task<ActionResult<ApiResponse<object>>> Imoveis([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastroLegado("sigov.tributario_imovel", busca, page, pageSize);

    [HttpGet("economicos")]
    public Task<ActionResult<ApiResponse<object>>> Economicos([FromQuery] string? busca = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) => ListarCadastroLegado("sigov.tributario_economico", busca, page, pageSize);

    [HttpGet("{resource:regex(^(contribuintes|iptu|iss|taxas|parcelas|arrecadacoes|dam|livro-eletronico|parcelamentos|nfse)$)}")]
    public Task<ActionResult<ApiResponse<object>>> ListarAvancado(string resource, [FromQuery] TributarioFiltro filtro) => ListarRecurso(resource, filtro);

    [HttpGet("{resource:regex(^(contribuintes|iptu|iss|taxas|parcelas|arrecadacoes|dam|livro-eletronico|parcelamentos|nfse)$)}/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> ObterAvancado(string resource, long id) => ObterRecurso(resource, id);

    [HttpPost("contribuintes")]
    public Task<ActionResult<ApiResponse<object>>> CriarContribuinteAvancado([FromBody] ContribuinteAvancadoRequest request) => CriarContribuinte(request);

    [HttpPut("contribuintes/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarContribuinteAvancado(long id, [FromBody] ContribuinteAvancadoRequest request) => AtualizarContribuinte(id, request);

    [HttpPost("iptu")]
    public Task<ActionResult<ApiResponse<object>>> CriarIptu([FromBody] IptuRequest request) => CriarLancamentoIptu(request);

    [HttpPut("iptu/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarIptu(long id, [FromBody] IptuRequest request) => AtualizarLancamentoIptu(id, request);

    [HttpPost("iss")]
    public Task<ActionResult<ApiResponse<object>>> CriarIss([FromBody] IssRequest request) => CriarLancamentoIss(request);

    [HttpPut("iss/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarIss(long id, [FromBody] IssRequest request) => AtualizarLancamentoIss(id, request);

    [HttpPost("taxas")]
    public Task<ActionResult<ApiResponse<object>>> CriarTaxa([FromBody] TaxaMunicipalRequest request) => CriarTaxaMunicipal(request);

    [HttpPut("taxas/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarTaxa(long id, [FromBody] TaxaMunicipalRequest request) => AtualizarTaxaMunicipal(id, request);

    [HttpPost("parcelamentos")]
    public Task<ActionResult<ApiResponse<object>>> CriarParcelamento([FromBody] ParcelamentoRequest request) => CriarParcelamentoDividaAtiva(request);

    [HttpPut("parcelamentos/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarParcelamento(long id, [FromBody] ParcelamentoRequest request) => AtualizarParcelamentoDividaAtiva(id, request);

    [HttpPost("parcelas")]
    public Task<ActionResult<ApiResponse<object>>> CriarParcela([FromBody] ParcelaRequest request) => CriarParcelaTributaria(request);

    [HttpPut("parcelas/{id:long}")]
    public Task<ActionResult<ApiResponse<object>>> AtualizarParcela(long id, [FromBody] ParcelaRequest request) => AtualizarParcelaTributaria(id, request);

    [HttpGet("arrecadacao/status")]
    public async Task<ActionResult<ApiResponse<object>>> StatusArrecadacao([FromQuery] DateOnly? inicio = null, [FromQuery] DateOnly? fim = null)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var status = await c.QueryAsync<object>(@"select status, count(*) quantidade, coalesce(sum(valor_atualizado),0) valor
from sigov.parcela
where tenant_id=@TenantId and (@Inicio is null or data_vencimento>=@Inicio) and (@Fim is null or data_vencimento<=@Fim)
group by status order by status", new { TenantId = tenantId, Inicio = inicio, Fim = fim });
            return Ok(ApiResponse<object>.Ok(new { status, inicio, fim, versao = "Pós-Build 08" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar status de arrecadação. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao consultar status de arrecadação.", cid));
        }
    }

    [HttpPost("arrecadacao/registrar")]
    public async Task<ActionResult<ApiResponse<object>>> RegistrarArrecadacao([FromBody] ArrecadacaoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            if (!await TemPermissao("arrecadacao", "registrar")) return Forbid();
            if (request.ValorPago <= 0) return BadRequest(ApiResponse<object>.Fail("Valor pago deve ser maior que zero.", cid));
            using var c = _context.CreateConnection();
            var parcela = await c.QuerySingleOrDefaultAsync<dynamic>("select id, contribuinte_id from sigov.parcela where id=@ParcelaId and tenant_id=@TenantId", new { request.ParcelaId, TenantId = tenantId });
            if (parcela is null) return NotFound(ApiResponse<object>.Fail("Parcela não encontrada para o tenant informado.", cid));
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.arrecadacao(tenant_id,parcela_id,contribuinte_id,valor_pago,forma_pagamento,status,codigo_baixa,correlation_id,usuario_id)
values(@TenantId,@ParcelaId,@ContribuinteId,@ValorPago,@FormaPagamento,'CONFIRMADA',@CodigoBaixa,@CorrelationId,@UsuarioId) returning id", new { TenantId = tenantId, request.ParcelaId, ContribuinteId = (long)parcela.contribuinte_id, request.ValorPago, request.FormaPagamento, CodigoBaixa = request.CodigoBaixa ?? $"BX-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}", CorrelationId = Guid.Parse(cid), UsuarioId = _currentUser.UsuarioId });
            await c.ExecuteAsync("update sigov.parcela set status='PAGA', updated_at=now() where id=@ParcelaId and tenant_id=@TenantId", new { request.ParcelaId, TenantId = tenantId });
            await Auditar(c, tenantId, "TRIBUTARIO_ARRECADACAO_REGISTRADA", request, cid);
            return Ok(ApiResponse<object>.Ok(new { id, status = "CONFIRMADA" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registrar arrecadação. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao registrar arrecadação.", cid));
        }
    }

    [HttpPost("dam/emitir")]
    public async Task<ActionResult<ApiResponse<object>>> EmitirDam([FromBody] EmitirDamRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            if (!await TemPermissao("arrecadacao", "registrar")) return Forbid();
            using var c = _context.CreateConnection();
            var parcela = await c.QuerySingleOrDefaultAsync<dynamic>(@"select p.id, p.contribuinte_id, p.valor_atualizado, p.data_vencimento, p.status, c.inscricao
from sigov.parcela p join sigov.contribuinte c on c.id=p.contribuinte_id and c.tenant_id=p.tenant_id
where p.id=@ParcelaId and p.tenant_id=@TenantId and c.ativo=true", new { request.ParcelaId, TenantId = tenantId });
            if (parcela is null) return BadRequest(ApiResponse<object>.Fail("Não é permitido emitir DAM sem contribuinte ativo ou inscrição válida.", cid));
            if (string.IsNullOrWhiteSpace((string)parcela.inscricao)) return BadRequest(ApiResponse<object>.Fail("Inscrição municipal do contribuinte é obrigatória para emissão do DAM.", cid));
            var numero = $"DAM-{tenantId}-{request.ParcelaId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            var linha = $"816{tenantId:D6}{request.ParcelaId:D10}{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.documento_arrecadacao_municipal(tenant_id,numero,parcela_id,contribuinte_id,linha_digitavel,codigo_barras,valor,data_vencimento,status,emissao_simulada,versao,historico_json,correlation_id,usuario_id)
values(@TenantId,@Numero,@ParcelaId,@ContribuinteId,@Linha,@Linha,@Valor,@Vencimento,'EMITIDO',true,1,cast(@Historico as jsonb),@CorrelationId,@UsuarioId)
on conflict(tenant_id,numero) do update set updated_at=now() returning id", new { TenantId = tenantId, Numero = numero, request.ParcelaId, ContribuinteId = (long)parcela.contribuinte_id, Linha = linha, Valor = (decimal)parcela.valor_atualizado, Vencimento = (DateTime)parcela.data_vencimento, Historico = JsonSerializer.Serialize(new[] { new { status = "EMITIDO", correlationId = cid, timestamp = DateTimeOffset.UtcNow } }, JsonOptions), CorrelationId = Guid.Parse(cid), UsuarioId = _currentUser.UsuarioId });
            await IntegrarContaReceber(c, tenantId, id, numero, (decimal)parcela.valor_atualizado, (DateTime)parcela.data_vencimento);
            await Auditar(c, tenantId, "TRIBUTARIO_DAM_SIMULADO_EMITIDO", new { request.ParcelaId, id, numero }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, numero, linhaDigitavel = linha, emissaoSimulada = true }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao emitir DAM simulado. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao emitir DAM simulado.", cid));
        }
    }

    [HttpPost("nfse/simular")]
    public async Task<ActionResult<ApiResponse<object>>> SimularNfse([FromBody] NfseRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            if (!await TemPermissao("nfse", "emitir")) return Forbid();
            if (string.IsNullOrWhiteSpace(request.InscricaoMunicipal)) return BadRequest(ApiResponse<object>.Fail("Inscrição municipal é obrigatória para NFS-e simulada.", cid));
            using var c = _context.CreateConnection();
            var contribuinteExiste = await c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.contribuinte where id=@ContribuinteId and tenant_id=@TenantId and ativo=true and inscricao is not null)", new { request.ContribuinteId, TenantId = tenantId });
            if (!contribuinteExiste) return BadRequest(ApiResponse<object>.Fail("Não é permitido simular NFS-e sem contribuinte ativo.", cid));
            var valorIss = Math.Round(request.ValorServico * request.Aliquota / 100m, 2);
            var nfseNumero = $"NFSE-SIM-{tenantId}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.integracao_nfse(tenant_id,contribuinte_id,inscricao_municipal,rps_numero,nfse_numero,competencia,valor_servico,valor_iss,status,payload_json,resposta_json,correlation_id,usuario_id)
values(@TenantId,@ContribuinteId,@InscricaoMunicipal,@RpsNumero,@NfseNumero,@Competencia,@ValorServico,@ValorIss,'SIMULADA',cast(@Payload as jsonb),cast(@Resposta as jsonb),@CorrelationId,@UsuarioId)
on conflict(tenant_id,inscricao_municipal,rps_numero) do update set nfse_numero=excluded.nfse_numero, valor_servico=excluded.valor_servico, valor_iss=excluded.valor_iss, resposta_json=excluded.resposta_json, updated_at=now() returning id", new { TenantId = tenantId, request.ContribuinteId, request.InscricaoMunicipal, request.RpsNumero, NfseNumero = nfseNumero, request.Competencia, request.ValorServico, ValorIss = valorIss, Payload = JsonSerializer.Serialize(request, JsonOptions), Resposta = JsonSerializer.Serialize(new { ambiente = "SIMULADO", nfseNumero, correlationId = cid }, JsonOptions), CorrelationId = Guid.Parse(cid), UsuarioId = _currentUser.UsuarioId });
            await Auditar(c, tenantId, "TRIBUTARIO_NFSE_SIMULADA", new { request.RpsNumero, nfseNumero, valorIss }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, nfseNumero, valorIss, status = "SIMULADA" }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao simular NFS-e. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao simular NFS-e.", cid));
        }
    }

    [HttpPost("livro-eletronico/gerar")]
    public async Task<ActionResult<ApiResponse<object>>> GerarLivro([FromBody] GerarLivroRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            if (!await TemPermissao("livro_eletronico", "gerar")) return Forbid();
            using var c = _context.CreateConnection();
            var totalLancado = await ScalarDecimal(c, "select coalesce((select sum(valor_lancado) from sigov.iptu where tenant_id=@TenantId and date_trunc('month', data_vencimento)=date_trunc('month', cast(@Competencia as date))),0) + coalesce((select sum(valor_lancado) from sigov.iss where tenant_id=@TenantId and date_trunc('month', competencia)=date_trunc('month', cast(@Competencia as date))),0) + coalesce((select sum(valor) from sigov.taxas_municipais where tenant_id=@TenantId and date_trunc('month', competencia)=date_trunc('month', cast(@Competencia as date))),0)", tenantId, request.Competencia);
            var totalArrecadado = await ScalarDecimal(c, "select coalesce(sum(valor_pago),0) from sigov.arrecadacao where tenant_id=@TenantId and date_trunc('month', data_pagamento)=date_trunc('month', cast(@Competencia as date))", tenantId, request.Competencia);
            var version = await c.ExecuteScalarAsync<int>("select coalesce(max(versao),0)+1 from sigov.livro_eletronico_tributario where tenant_id=@TenantId and competencia=@Competencia and tipo=@Tipo", new { TenantId = tenantId, request.Competencia, request.Tipo });
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.livro_eletronico_tributario(tenant_id,competencia,tipo,versao,status,total_lancado,total_arrecadado,registros_json,historico_json,gerado_por,correlation_id)
values(@TenantId,@Competencia,@Tipo,@Versao,'GERADO',@TotalLancado,@TotalArrecadado,cast(@Registros as jsonb),cast(@Historico as jsonb),@UsuarioId,@CorrelationId) returning id", new { TenantId = tenantId, request.Competencia, request.Tipo, Versao = version, TotalLancado = totalLancado, TotalArrecadado = totalArrecadado, Registros = "[]", Historico = JsonSerializer.Serialize(new[] { new { versao = version, acao = "GERADO", correlationId = cid, timestamp = DateTimeOffset.UtcNow } }, JsonOptions), UsuarioId = _currentUser.UsuarioId, CorrelationId = Guid.Parse(cid) });
            await Auditar(c, tenantId, "TRIBUTARIO_LIVRO_ELETRONICO_GERADO", new { id, request.Competencia, request.Tipo, version }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, versao = version, totalLancado, totalArrecadado }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar livro eletrônico. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao gerar livro eletrônico.", cid));
        }
    }

    [HttpPost("dam-dev")]
    public ActionResult<ApiResponse<object>> GerarDamDev([FromBody] DevIntegrationRequest request) => DevOnly("DAM fake", request);

    [HttpPost("pix-dev")]
    public ActionResult<ApiResponse<object>> GerarPixDev([FromBody] DevIntegrationRequest request) => DevOnly("PIX dev", request);

    [HttpPost("pagamentos-dev")]
    public ActionResult<ApiResponse<object>> RegistrarPagamentoDev([FromBody] DevIntegrationRequest request) => DevOnly("pagamento dev", request);

    private async Task<ActionResult<ApiResponse<object>>> ListarRecurso(string resource, TributarioFiltro filtro)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            var meta = Resources[resource];
            var (recursoPermissao, acaoPermissao) = PermissaoParaListagem(resource);
            if (!await TemPermissao(recursoPermissao, acaoPermissao)) return Forbid();
            var page = Math.Max(1, filtro.Page);
            var pageSize = Math.Clamp(filtro.PageSize, 1, 100);
            var sql = $@"select * from {meta.Table}
where tenant_id=@TenantId
  and (@Status is null or cast({meta.StatusColumn} as text)=@Status)
  and (@Busca is null or cast({meta.SearchColumn} as text) ilike '%' || @Busca || '%')
order by {meta.OrderBy}
limit @PageSize offset @Offset";
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>(sql, new { TenantId = tenantId, filtro.Status, filtro.Busca, PageSize = pageSize, Offset = (page - 1) * pageSize });
            var total = await c.ExecuteScalarAsync<int>($"select count(*) from {meta.Table} where tenant_id=@TenantId", new { TenantId = tenantId });
            return Ok(ApiResponse<object>.Ok(new { items = rows, total, page, pageSize, filtro.Status, filtro.Busca }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar recurso tributário {Resource}. CorrelationId={CorrelationId}", resource, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar recurso tributário.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> ObterRecurso(string resource, long id)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            var meta = Resources[resource];
            var (recursoPermissao, acaoPermissao) = PermissaoParaListagem(resource);
            if (!await TemPermissao(recursoPermissao, acaoPermissao)) return Forbid();
            using var c = _context.CreateConnection();
            var row = await c.QuerySingleOrDefaultAsync<object>($"select * from {meta.Table} where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            return row is null ? NotFound(ApiResponse<object>.Fail("Registro não encontrado para o tenant informado.", cid)) : Ok(ApiResponse<object>.Ok(row, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter recurso tributário {Resource}. CorrelationId={CorrelationId}", resource, cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao obter recurso tributário.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> CriarContribuinte(ContribuinteAvancadoRequest request)
    {
        var cid = CorrelationId();
        if (string.IsNullOrWhiteSpace(request.Inscricao)) return BadRequest(ApiResponse<object>.Fail("Inscrição é obrigatória.", cid));
        if (string.IsNullOrWhiteSpace(request.Nome)) return BadRequest(ApiResponse<object>.Fail("Nome é obrigatório.", cid));
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.contribuinte(tenant_id,inscricao,nome,documento,tipo_pessoa,email,telefone,endereco_json,consentimento_lgpd,ativo)
values(@TenantId,@Inscricao,@Nome,@Documento,@TipoPessoa,@Email,@Telefone,cast(@EnderecoJson as jsonb),@ConsentimentoLgpd,true) returning id", new { TenantId = tenantId, request.Inscricao, request.Nome, request.Documento, request.TipoPessoa, request.Email, request.Telefone, EnderecoJson = request.EnderecoJson ?? "{}", request.ConsentimentoLgpd });
            await Auditar(c, tenantId, "TRIBUTARIO_CONTRIBUINTE_CRIADO", MaskRequest(request), cid);
            return Created($"/api/tributario/contribuintes/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar contribuinte avançado. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar contribuinte.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarContribuinte(long id, ContribuinteAvancadoRequest request)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var affected = await c.ExecuteAsync(@"update sigov.contribuinte set inscricao=@Inscricao,nome=@Nome,documento=@Documento,tipo_pessoa=@TipoPessoa,email=@Email,telefone=@Telefone,endereco_json=cast(@EnderecoJson as jsonb),consentimento_lgpd=@ConsentimentoLgpd,updated_at=now()
where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, request.Inscricao, request.Nome, request.Documento, request.TipoPessoa, request.Email, request.Telefone, EnderecoJson = request.EnderecoJson ?? "{}", request.ConsentimentoLgpd });
            if (affected == 0) return NotFound(ApiResponse<object>.Fail("Contribuinte não encontrado para o tenant informado.", cid));
            await Auditar(c, tenantId, "TRIBUTARIO_CONTRIBUINTE_ATUALIZADO", MaskRequest(request), cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar contribuinte avançado. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar contribuinte.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> CriarLancamentoIptu(IptuRequest r)
    {
        var cid = CorrelationId();
        if (string.IsNullOrWhiteSpace(r.InscricaoImobiliaria)) return BadRequest(ApiResponse<object>.Fail("Inscrição imobiliária é obrigatória.", cid));
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await ContribuinteValido(c, tenantId, r.ContribuinteId)) return BadRequest(ApiResponse<object>.Fail("Contribuinte válido é obrigatório.", cid));
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.iptu(tenant_id,inscricao_imobiliaria,contribuinte_id,exercicio,valor_venal,aliquota,valor_lancado,data_vencimento,status,dados_json)
values(@TenantId,@InscricaoImobiliaria,@ContribuinteId,@Exercicio,@ValorVenal,@Aliquota,@ValorLancado,@DataVencimento,@Status,cast(@DadosJson as jsonb)) returning id", new { TenantId = tenantId, r.InscricaoImobiliaria, r.ContribuinteId, r.Exercicio, r.ValorVenal, r.Aliquota, r.ValorLancado, r.DataVencimento, Status = r.Status ?? "ABERTO", DadosJson = r.DadosJson ?? "{}" });
            await CriarParcelaFinanceira(c, tenantId, "IPTU", id, r.ContribuinteId, 1, r.ValorLancado, r.DataVencimento, $"IPTU {r.InscricaoImobiliaria}/{r.Exercicio}");
            await Auditar(c, tenantId, "TRIBUTARIO_IPTU_CRIADO", r, cid);
            return Created($"/api/tributario/iptu/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar IPTU. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar IPTU.", cid));
        }
    }

    private Task<ActionResult<ApiResponse<object>>> AtualizarLancamentoIptu(long id, IptuRequest r) => AtualizarLancamento("sigov.iptu", id, r, "TRIBUTARIO_IPTU_ATUALIZADO");

    private async Task<ActionResult<ApiResponse<object>>> CriarLancamentoIss(IssRequest r)
    {
        var cid = CorrelationId();
        if (string.IsNullOrWhiteSpace(r.InscricaoMunicipal)) return BadRequest(ApiResponse<object>.Fail("Inscrição municipal é obrigatória.", cid));
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await ContribuinteValido(c, tenantId, r.ContribuinteId)) return BadRequest(ApiResponse<object>.Fail("Contribuinte válido é obrigatório.", cid));
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.iss(tenant_id,inscricao_municipal,contribuinte_id,competencia,base_calculo,aliquota,valor_lancado,data_vencimento,status,origem,origem_id)
values(@TenantId,@InscricaoMunicipal,@ContribuinteId,@Competencia,@BaseCalculo,@Aliquota,@ValorLancado,@DataVencimento,@Status,@Origem,@OrigemId) returning id", new { TenantId = tenantId, r.InscricaoMunicipal, r.ContribuinteId, r.Competencia, r.BaseCalculo, r.Aliquota, r.ValorLancado, r.DataVencimento, Status = r.Status ?? "ABERTO", r.Origem, r.OrigemId });
            await CriarParcelaFinanceira(c, tenantId, "ISS", id, r.ContribuinteId, 1, r.ValorLancado, r.DataVencimento, $"ISS {r.InscricaoMunicipal} {r.Competencia:yyyy-MM}");
            await Auditar(c, tenantId, "TRIBUTARIO_ISS_CRIADO", r, cid);
            return Created($"/api/tributario/iss/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar ISS. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar ISS.", cid));
        }
    }

    private Task<ActionResult<ApiResponse<object>>> AtualizarLancamentoIss(long id, IssRequest r) => AtualizarLancamento("sigov.iss", id, r, "TRIBUTARIO_ISS_ATUALIZADO");

    private async Task<ActionResult<ApiResponse<object>>> CriarTaxaMunicipal(TaxaMunicipalRequest r)
    {
        var cid = CorrelationId();
        if (string.IsNullOrWhiteSpace(r.Inscricao)) return BadRequest(ApiResponse<object>.Fail("Inscrição é obrigatória.", cid));
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await ContribuinteValido(c, tenantId, r.ContribuinteId)) return BadRequest(ApiResponse<object>.Fail("Contribuinte válido é obrigatório.", cid));
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.taxas_municipais(tenant_id,codigo,descricao,contribuinte_id,inscricao,competencia,valor,data_vencimento,status)
values(@TenantId,@Codigo,@Descricao,@ContribuinteId,@Inscricao,@Competencia,@Valor,@DataVencimento,@Status) returning id", new { TenantId = tenantId, r.Codigo, r.Descricao, r.ContribuinteId, r.Inscricao, r.Competencia, r.Valor, r.DataVencimento, Status = r.Status ?? "ABERTO" });
            await CriarParcelaFinanceira(c, tenantId, "TAXA", id, r.ContribuinteId, 1, r.Valor, r.DataVencimento, $"Taxa {r.Codigo} {r.Inscricao}");
            await Auditar(c, tenantId, "TRIBUTARIO_TAXA_CRIADA", r, cid);
            return Created($"/api/tributario/taxas/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar taxa municipal. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar taxa municipal.", cid));
        }
    }

    private Task<ActionResult<ApiResponse<object>>> AtualizarTaxaMunicipal(long id, TaxaMunicipalRequest r) => AtualizarLancamento("sigov.taxas_municipais", id, r, "TRIBUTARIO_TAXA_ATUALIZADA");

    private async Task<ActionResult<ApiResponse<object>>> CriarParcelamentoDividaAtiva(ParcelamentoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            if (!await ContribuinteValido(c, tenantId, r.ContribuinteId)) return BadRequest(ApiResponse<object>.Fail("Contribuinte válido é obrigatório.", cid));
            var id = await c.ExecuteScalarAsync<long>(@"insert into sigov.parcelamento_divida_ativa(tenant_id,numero,contribuinte_id,inscricao_divida,valor_original,valor_atualizado,quantidade_parcelas,status,termo_json)
values(@TenantId,@Numero,@ContribuinteId,@InscricaoDivida,@ValorOriginal,@ValorAtualizado,@QuantidadeParcelas,@Status,cast(@TermoJson as jsonb)) returning id", new { TenantId = tenantId, r.Numero, r.ContribuinteId, r.InscricaoDivida, r.ValorOriginal, r.ValorAtualizado, r.QuantidadeParcelas, Status = r.Status ?? "ATIVO", TermoJson = r.TermoJson ?? "{}" });
            var valorParcela = Math.Round(r.ValorAtualizado / r.QuantidadeParcelas, 2);
            for (var numero = 1; numero <= r.QuantidadeParcelas; numero++)
            {
                await CriarParcelaFinanceira(c, tenantId, "DIVIDA_ATIVA", id, r.ContribuinteId, numero, valorParcela, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(numero)), $"Parcelamento dívida ativa {r.Numero}/{numero}");
            }
            await Auditar(c, tenantId, "TRIBUTARIO_PARCELAMENTO_CRIADO", r, cid);
            return Created($"/api/tributario/parcelamentos/{id}", ApiResponse<object>.Ok(new { id, parcelas = r.QuantidadeParcelas }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar parcelamento. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar parcelamento.", cid));
        }
    }

    private Task<ActionResult<ApiResponse<object>>> AtualizarParcelamentoDividaAtiva(long id, ParcelamentoRequest r) => AtualizarLancamento("sigov.parcelamento_divida_ativa", id, r, "TRIBUTARIO_PARCELAMENTO_ATUALIZADO");

    private async Task<ActionResult<ApiResponse<object>>> CriarParcelaTributaria(ParcelaRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var id = await CriarParcelaFinanceira(c, tenantId, r.OrigemTipo, r.OrigemId, r.ContribuinteId, r.Numero, r.ValorAtualizado, r.DataVencimento, $"Parcela tributária {r.OrigemTipo}/{r.OrigemId}/{r.Numero}");
            await Auditar(c, tenantId, "TRIBUTARIO_PARCELA_CRIADA", r, cid);
            return Created($"/api/tributario/parcelas/{id}", ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar parcela tributária. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar parcela tributária.", cid));
        }
    }

    private Task<ActionResult<ApiResponse<object>>> AtualizarParcelaTributaria(long id, ParcelaRequest r) => AtualizarLancamento("sigov.parcela", id, r, "TRIBUTARIO_PARCELA_ATUALIZADA");

    private async Task<ActionResult<ApiResponse<object>>> AtualizarLancamento(string table, long id, object payload, string evento)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync($"update {table} set updated_at=now() where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId });
            await Auditar(c, tenantId, evento, payload, cid);
            return Ok(ApiResponse<object>.Ok(new { id, atualizado = true }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar lançamento tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar lançamento tributário.", cid));
        }
    }

    private async Task<long> CriarParcelaFinanceira(IDbConnection c, long tenantId, string origemTipo, long origemId, long contribuinteId, int numero, decimal valor, DateOnly vencimento, string descricao)
    {
        var parcelaId = await c.ExecuteScalarAsync<long>(@"insert into sigov.parcela(tenant_id,origem_tipo,origem_id,contribuinte_id,numero,valor_original,valor_atualizado,data_vencimento,status)
values(@TenantId,@OrigemTipo,@OrigemId,@ContribuinteId,@Numero,@Valor,@Valor,@Vencimento,'ABERTA')
on conflict(tenant_id,origem_tipo,origem_id,numero) do update set valor_atualizado=excluded.valor_atualizado, data_vencimento=excluded.data_vencimento, updated_at=now() returning id", new { TenantId = tenantId, OrigemTipo = origemTipo, OrigemId = origemId, ContribuinteId = contribuinteId, Numero = numero, Valor = valor, Vencimento = vencimento });
        var contaId = await c.ExecuteScalarAsync<long>(@"insert into sigov.financeiro_conta_receber(tenant_id,origem,origem_id,numero_documento,parcela,descricao,valor_original,valor_aberto,vencimento,status)
values(@TenantId,'TRIBUTARIO',@ParcelaId,@NumeroDocumento,@Numero,@Descricao,@Valor,@Valor,@Vencimento,'ABERTA') returning id", new { TenantId = tenantId, ParcelaId = parcelaId, NumeroDocumento = $"TRIB-{origemTipo}-{origemId}-{numero}", Numero = numero, Descricao = descricao, Valor = valor, Vencimento = vencimento });
        await c.ExecuteAsync("update sigov.parcela set conta_receber_id=@ContaId where id=@ParcelaId and tenant_id=@TenantId", new { ContaId = contaId, ParcelaId = parcelaId, TenantId = tenantId });
        return parcelaId;
    }

    private static Task IntegrarContaReceber(IDbConnection c, long tenantId, long damId, string numero, decimal valor, DateTime vencimento) => c.ExecuteAsync(@"insert into sigov.financeiro_conta_receber(tenant_id,origem,origem_id,numero_documento,parcela,descricao,valor_original,valor_aberto,vencimento,status)
values(@TenantId,'DAM',@DamId,@Numero,1,@Descricao,@Valor,@Valor,@Vencimento,'ABERTA')", new { TenantId = tenantId, DamId = damId, Numero = numero, Descricao = $"DAM simulado {numero}", Valor = valor, Vencimento = DateOnly.FromDateTime(vencimento) });

    private static Task<bool> ContribuinteValido(IDbConnection c, long tenantId, long contribuinteId) => c.ExecuteScalarAsync<bool>("select exists(select 1 from sigov.contribuinte where id=@ContribuinteId and tenant_id=@TenantId and ativo=true and inscricao is not null and inscricao <> '')", new { ContribuinteId = contribuinteId, TenantId = tenantId });

    private async Task<ActionResult<ApiResponse<object>>> ListarLegado(string tabela, string order, string? tipo)
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
            _logger.LogError(ex, "Erro ao listar tributário legado. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar dados tributários.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> InserirTipo(TipoCadastroRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.tributario_tipo_cadastro(tenant_id,codigo,nome,descricao) values(@TenantId,@Codigo,@Nome,@Descricao) returning id", new { TenantId = tenantId, r.Codigo, r.Nome, r.Descricao });
            await Auditar(c, tenantId, "TRIBUTARIO_TIPO_CRIADO", r, cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar tipo tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar tipo.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarTipoCadastro(long id, TipoCadastroRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.tributario_tipo_cadastro set codigo=@Codigo,nome=@Nome,descricao=@Descricao where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.Codigo, r.Nome, r.Descricao });
            await Auditar(c, tenantId, "TRIBUTARIO_TIPO_ATUALIZADO", r, cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar tipo tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar tipo.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> InserirCampo(CampoDinamicoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            var id = await c.ExecuteScalarAsync<long>("insert into sigov.tributario_campo_dinamico(tenant_id,tipo_cadastro_codigo,codigo,nome,tipo,obrigatorio,ordem,opcoes_json) values(@TenantId,@TipoCadastroCodigo,@Codigo,@Nome,@Tipo,@Obrigatorio,@Ordem,cast(@OpcoesJson as jsonb)) returning id", new { TenantId = tenantId, r.TipoCadastroCodigo, r.Codigo, r.Nome, r.Tipo, r.Obrigatorio, r.Ordem, OpcoesJson = r.OpcoesJson ?? "[]" });
            await Auditar(c, tenantId, "TRIBUTARIO_CAMPO_CRIADO", r, cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar campo tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao criar campo.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> AtualizarCampoDinamico(long id, CampoDinamicoRequest r)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync("update sigov.tributario_campo_dinamico set tipo_cadastro_codigo=@TipoCadastroCodigo,codigo=@Codigo,nome=@Nome,tipo=@Tipo,obrigatorio=@Obrigatorio,ordem=@Ordem,opcoes_json=cast(@OpcoesJson as jsonb) where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, r.TipoCadastroCodigo, r.Codigo, r.Nome, r.Tipo, r.Obrigatorio, r.Ordem, OpcoesJson = r.OpcoesJson ?? "[]" });
            await Auditar(c, tenantId, "TRIBUTARIO_CAMPO_ATUALIZADO", r, cid);
            return Ok(ApiResponse<object>.Ok(new { id }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar campo tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao atualizar campo.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarContribuintesLegado(string? busca, int page, int pageSize)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>(@"select id, tenant_id, nome, case when documento is null then null else repeat('*', greatest(length(documento)-4,0)) || right(documento,4) end documento, email, telefone, tipo_pessoa, ativo, created_at, updated_at
from sigov.tributario_contribuinte
where tenant_id=@TenantId and (@Busca is null or nome ilike '%' || @Busca || '%' or documento ilike '%' || @Busca || '%')
order by nome limit @PageSize offset @Offset", new { TenantId = tenantId, Busca = busca, PageSize = pageSize, Offset = (page - 1) * pageSize });
            return Ok(ApiResponse<object>.Ok(new { items = rows, page, pageSize }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar contribuintes legados. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar contribuintes.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> ListarCadastroLegado(string table, string? busca, int page, int pageSize)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);
            using var c = _context.CreateConnection();
            var rows = await c.QueryAsync<object>($"select * from {table} where tenant_id=@TenantId and (@Busca is null or inscricao ilike '%' || @Busca || '%') order by inscricao limit @PageSize offset @Offset", new { TenantId = tenantId, Busca = busca, PageSize = pageSize, Offset = (page - 1) * pageSize });
            return Ok(ApiResponse<object>.Ok(new { items = rows, page, pageSize }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar cadastro tributário legado. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao listar cadastro tributário.", cid));
        }
    }

    private async Task<ActionResult<ApiResponse<object>>> Status(string tabela, long id, bool ativo, string evento)
    {
        var cid = CorrelationId();
        try
        {
            var tenantId = RequireTenant();
            using var c = _context.CreateConnection();
            await c.ExecuteAsync($"update {tabela} set ativo=@Ativo where id=@Id and tenant_id=@TenantId", new { Id = id, TenantId = tenantId, Ativo = ativo });
            await Auditar(c, tenantId, evento, new { id, ativo }, cid);
            return Ok(ApiResponse<object>.Ok(new { id, ativo }, correlationId: cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar status tributário. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao alterar status.", cid));
        }
    }

    private static Task<int> Count(IDbConnection c, string table, long tenantId, string predicate) => c.ExecuteScalarAsync<int>($"select count(*) from {table} where tenant_id=@TenantId and {predicate}", new { TenantId = tenantId });

    private static Task<decimal> ScalarDecimal(IDbConnection c, string sql, long tenantId, DateOnly? competencia = null) => c.ExecuteScalarAsync<decimal>(sql, new { TenantId = tenantId, Competencia = competencia });


    private async Task<bool> TemPermissao(string recurso, string acao)
    {
        if (!_currentUser.UsuarioId.HasValue) return true;
        return await _permissions.HasPermissionAsync(_currentUser.UsuarioId.Value, "tributario", recurso, acao, HttpContext.RequestAborted).ConfigureAwait(false);
    }

    private static (string Recurso, string Acao) PermissaoParaListagem(string resource) => resource switch
    {
        "iptu" => ("iptu", "visualizar"),
        "iss" => ("iss", "visualizar"),
        "taxas" => ("taxas", "visualizar"),
        "parcelamentos" => ("parcelamento", "visualizar"),
        "arrecadacoes" => ("arrecadacao", "visualizar"),
        "nfse" => ("nfse", "visualizar"),
        "livro-eletronico" => ("livro_eletronico", "visualizar"),
        "dam" => ("arrecadacao", "visualizar"),
        _ => ("dashboard", "visualizar")
    };

    private long RequireTenant() => _currentTenant.TenantId ?? throw new InvalidOperationException("Tenant obrigatório para operar Tributário.");

    private string CorrelationId()
    {
        if (Guid.TryParse(HttpContext.TraceIdentifier, out _)) return HttpContext.TraceIdentifier;
        var current = HttpContext.Request.Headers.TryGetValue("X-Correlation-Id", out var value) && Guid.TryParse(value, out _) ? value.ToString() : Guid.NewGuid().ToString();
        HttpContext.TraceIdentifier = current;
        return current;
    }

    private Task Auditar(IDbConnection c, long tenantId, string tipo, object payload, string cid) => c.ExecuteAsync(@"insert into sigov.saas_evento_comercial(tenant_id,tipo_evento,descricao,origem,usuario_id,payload,correlation_id)
values(@TenantId,@Tipo,@Tipo,'tributario',@UsuarioId,cast(@Payload as jsonb),@CorrelationId)", new { TenantId = tenantId, Tipo = tipo, UsuarioId = _currentUser.UsuarioId, Payload = JsonSerializer.Serialize(new { payload, tenantId, usuarioId = _currentUser.UsuarioId, correlationId = cid, timestamp = DateTimeOffset.UtcNow }, JsonOptions), CorrelationId = Guid.Parse(cid) });

    private static object MaskRequest(ContribuinteAvancadoRequest request) => new { request.Inscricao, request.Nome, documento = request.Documento is null ? null : $"***{request.Documento[^Math.Min(4, request.Documento.Length)..]}", request.TipoPessoa, request.ConsentimentoLgpd };

    private ActionResult<ApiResponse<object>> DevOnly(string recurso, DevIntegrationRequest request)
    {
        if (!_environment.IsDevelopment()) return StatusCode(StatusCodes.Status422UnprocessableEntity, ApiResponse<object>.Fail($"{recurso} disponível somente em Development. Integração real não configurada para este ambiente."));
        return Ok(ApiResponse<object>.Ok(new { request.ParcelaId, request.Valor, ambiente = _environment.EnvironmentName }));
    }
}

public sealed record TributarioResource(string Table, string StatusColumn, string OrderBy, string SearchColumn, string PermissionPrefix);
public sealed record TributarioFiltro(string? Busca = null, string? Status = null, int Page = 1, int PageSize = 20);
public sealed record TributarioConfiguracaoRequest(string? InscricaoImobiliariaMascara, string? InscricaoMobiliariaMascara, bool UsaGeorreferenciamento, bool UsaIntegracaoNfse, bool UsaProtesto);
public sealed record TipoCadastroRequest(string Codigo, string Nome, string? Descricao);
public sealed record CampoDinamicoRequest(string TipoCadastroCodigo, string Codigo, string Nome, string Tipo, bool Obrigatorio, int Ordem, string? OpcoesJson);
public sealed record StatusAtivoRequest(bool Ativo);
public sealed record ContribuinteAvancadoRequest(string Inscricao, string Nome, string? Documento, string TipoPessoa, string? Email, string? Telefone, string? EnderecoJson, bool ConsentimentoLgpd);
public sealed record IptuRequest(string InscricaoImobiliaria, long ContribuinteId, int Exercicio, decimal ValorVenal, decimal Aliquota, decimal ValorLancado, DateOnly DataVencimento, string? Status, string? DadosJson);
public sealed record IssRequest(string InscricaoMunicipal, long ContribuinteId, DateOnly Competencia, decimal BaseCalculo, decimal Aliquota, decimal ValorLancado, DateOnly DataVencimento, string? Status, string? Origem, long? OrigemId);
public sealed record TaxaMunicipalRequest(string Codigo, string Descricao, long ContribuinteId, string Inscricao, DateOnly Competencia, decimal Valor, DateOnly DataVencimento, string? Status);
public sealed record ParcelaRequest(string OrigemTipo, long OrigemId, long ContribuinteId, int Numero, decimal ValorAtualizado, DateOnly DataVencimento);
public sealed record ParcelamentoRequest(string Numero, long ContribuinteId, string InscricaoDivida, decimal ValorOriginal, decimal ValorAtualizado, int QuantidadeParcelas, string? Status, string? TermoJson);
public sealed record ArrecadacaoRequest(long ParcelaId, decimal ValorPago, string FormaPagamento, string? CodigoBaixa);
public sealed record EmitirDamRequest(long ParcelaId);
public sealed record NfseRequest(long ContribuinteId, string InscricaoMunicipal, string RpsNumero, DateOnly Competencia, decimal ValorServico, decimal Aliquota);
public sealed record GerarLivroRequest(DateOnly Competencia, string Tipo);
public sealed record DevIntegrationRequest(long ParcelaId, decimal? Valor);
