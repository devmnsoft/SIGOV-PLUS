using System.Text;
using System.Text.Json;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Tributario.TributarioAvancado;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Tributario;

public sealed class TributarioAvancadoRepository : ITributarioCarnesBoletosRepository, IPortalContribuinteRepository,
    ITributarioFiscalizacaoRepository, ITributarioNfseRepository, ITributarioCarnesBoletosService,
    ITributarioCarneEntregaService, ITributarioDamService, IPortalContribuinteCertidaoService,
    IPortalContribuinteGuiaService, IPortalContribuinteParcelamentoService, ITributarioIssqnService,
    ITributarioSimplesNacionalService, ITributarioAutoInfracaoService, ITributarioLivroEletronicoService,
    ITributarioDesifService, ITributarioNfseValidacaoService, ITributarioCarneArquivoService
{
    private static readonly HashSet<string> Recursos = new(StringComparer.Ordinal)
    {
        "tributario_carne_emissao", "tributario_carne_producao", "tributario_carne_entrega", "tributario_dam", "tributario_boleto_preparatorio",
        "portal_contribuinte_solicitacao", "portal_contribuinte_certidao", "portal_contribuinte_guia_emitida", "portal_contribuinte_parcelamento_solicitacao", "portal_contribuinte_protocolo",
        "tributario_fiscalizacao_ordem", "tributario_fiscalizacao_diligencia", "tributario_fiscalizacao_notificacao", "tributario_fiscalizacao_auto_infracao", "tributario_fiscalizacao_defesa", "tributario_fiscalizacao_julgamento", "tributario_simples_divergencia", "tributario_iss_apuracao",
        "tributario_nfse_configuracao", "tributario_nfse_nota", "tributario_livro_eletronico", "tributario_desif_declaracao"
    };
    private readonly DapperContext _context;
    public TributarioAvancadoRepository(DapperContext context) => _context = context;

    public async Task<PagedResult<TributarioRegistroDto>> ListarAsync(long tenantId, string recurso, int pagina, int tamanho, CancellationToken ct)
    {
        recurso = Recurso(recurso); pagina = Math.Max(1, pagina); tamanho = Math.Clamp(tamanho, 1, 100);
        var sql = $"select id as Id,codigo as Codigo,status as Status,tipo as Tipo,descricao as Descricao,valor as Valor,created_at as CreatedAt,dados::text as Dados from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false order by id desc limit @Tamanho offset @Offset; select count(1) from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false";
        using var c = _context.CreateConnection();
        using var m = await c.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, Tamanho = tamanho, Offset = (pagina - 1) * tamanho }, cancellationToken: ct));
        var rows = (await m.ReadAsync<Row>()).Select(Mapear).ToList();
        return new PagedResult<TributarioRegistroDto>(rows, pagina, tamanho, await m.ReadSingleAsync<long>());
    }
    public async Task<TributarioRegistroDto?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct)
    {
        recurso = Recurso(recurso); using var c = _context.CreateConnection();
        var row = await c.QuerySingleOrDefaultAsync<Row>(new CommandDefinition($"select id as Id,codigo as Codigo,status as Status,tipo as Tipo,descricao as Descricao,valor as Valor,created_at as CreatedAt,dados::text as Dados from sigov.{recurso} where tenant_id=@TenantId and id=@Id and is_deleted=false", new { TenantId = tenantId, Id = id }, cancellationToken: ct));
        return row is null ? null : Mapear(row);
    }
    public async Task<long> CriarAsync(TributarioAvancadoContext x, string recurso, TributarioOperacaoRequest r, CancellationToken ct)
    {
        recurso = Recurso(recurso); Validar(recurso, r);
        const string values = "(tenant_id,entidade_id,exercicio_id,referencia_id,codigo,tipo,status,descricao,justificativa,quantidade,valor,dados,auditoria,correlation_id,created_by) values(@TenantId,@EntidadeId,@ExercicioId,@ReferenciaId,@Codigo,@Tipo,@Status,@Descricao,@Justificativa,@Quantidade,@Valor,@Dados::jsonb,jsonb_build_object('acao','CRIAR','em',now()),@CorrelationId,@UsuarioId) returning id";
        using var c = _context.CreateConnection();
        return await c.ExecuteScalarAsync<long>(new CommandDefinition($"insert into sigov.{recurso}{values}", new { x.TenantId, x.EntidadeId, x.ExercicioId, r.ReferenciaId, r.Codigo, r.Tipo, r.Status, r.Descricao, r.Justificativa, r.Quantidade, r.Valor, Dados = JsonSerializer.Serialize(r.Dados ?? new Dictionary<string, object?>()), CorrelationId = Guid.TryParse(x.CorrelationId, out var g) ? g : Guid.NewGuid(), x.UsuarioId }, cancellationToken: ct));
    }
    public async Task<bool> AlterarStatusAsync(TributarioAvancadoContext x, string recurso, long id, string status, string? justificativa, CancellationToken ct)
    {
        recurso = Recurso(recurso);
        if ((status is "CANCELADO" or "CANCELADA") && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Cancelamento exige justificativa.");
        if (recurso == "tributario_carne_entrega" && status == "NOVA_TENTATIVA" && string.IsNullOrWhiteSpace(justificativa)) throw new ArgumentException("Nova tentativa exige motivo.");
        using var c = _context.CreateConnection();
        return await c.ExecuteAsync(new CommandDefinition($"update sigov.{recurso} set status=@Status,justificativa=coalesce(@Justificativa,justificativa),updated_at=now(),updated_by=@UsuarioId,correlation_id=@CorrelationId,auditoria=auditoria||jsonb_build_object('acao','STATUS','status',@Status,'em',now()) where tenant_id=@TenantId and id=@Id and is_deleted=false", new { x.TenantId, Id = id, Status = status, Justificativa = justificativa, x.UsuarioId, CorrelationId = Guid.TryParse(x.CorrelationId, out var g) ? g : Guid.NewGuid() }, cancellationToken: ct)) > 0;
    }
    public async Task<TributarioDashboardDto> DashboardAsync(long tenantId, string recurso, CancellationToken ct)
    {
        recurso = Recurso(recurso); using var c = _context.CreateConnection();
        var k = await c.QuerySingleAsync<Kpi>(new CommandDefinition($"select count(1) as Total,count(1) filter(where status in ('RASCUNHO','PENDENTE','ABERTA','EM_PRODUCAO')) as Pendentes,count(1) filter(where status in ('CONCLUIDO','ENTREGUE','EMITIDA','VALIDADO')) as Concluidos,count(1) filter(where prazo_at<now() and status not in ('CONCLUIDO','ENTREGUE','CANCELADO')) as Alertas from sigov.{recurso} where tenant_id=@TenantId and is_deleted=false", new { TenantId = tenantId }, cancellationToken: ct));
        var recentes = await ListarAsync(tenantId, recurso, 1, 6, ct); return new(k.Total, k.Pendentes, k.Concluidos, k.Alertas, recentes.Items);
    }
    public async Task<byte[]> GerarCsvAsync(long tenantId, long emissaoId, CancellationToken ct)
    {
        using var c = _context.CreateConnection();
        var rows = await c.QueryAsync<(long Id, string? Codigo, string Status)>(new CommandDefinition("select id as Id,codigo as Codigo,status as Status from sigov.tributario_carne_item where tenant_id=@TenantId and referencia_id=@EmissaoId and is_deleted=false order by id", new { TenantId = tenantId, EmissaoId = emissaoId }, cancellationToken: ct));
        var csv = new StringBuilder("id;codigo;status\n"); foreach (var row in rows) csv.Append(row.Id).Append(';').Append(row.Codigo?.Replace(";", string.Empty, StringComparison.Ordinal)).Append(';').Append(row.Status).Append('\n');
        return Encoding.UTF8.GetBytes(csv.ToString());
    }
    private static void Validar(string recurso, TributarioOperacaoRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Status)) throw new ArgumentException("Status é obrigatório.");
        if (recurso == "tributario_nfse_nota" && (!r.Valor.HasValue || r.Valor <= 0)) throw new ArgumentException("NFS-e preparatória exige valor positivo.");
        if (recurso == "tributario_fiscalizacao_auto_infracao" && (string.IsNullOrWhiteSpace(r.Descricao) || !r.Valor.HasValue || r.Valor <= 0)) throw new ArgumentException("Auto de infração exige fundamento, descrição e valor.");
    }
    private static string Recurso(string recurso) => Recursos.Contains(recurso) ? recurso : throw new ArgumentException("Recurso tributário inválido.");
    private static TributarioRegistroDto Mapear(Row r) => new(r.Id, r.Codigo, r.Status, r.Tipo, r.Descricao, r.Valor, r.CreatedAt, JsonSerializer.Deserialize<Dictionary<string, object?>>(r.Dados) ?? new());
    private sealed record Row(long Id, string? Codigo, string Status, string? Tipo, string? Descricao, decimal? Valor, DateTimeOffset CreatedAt, string Dados);
    private sealed record Kpi(long Total, long Pendentes, long Concluidos, long Alertas);
}
