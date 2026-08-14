using Dapper;
using Sigov.Application.Relatorios;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Relatorios;

public sealed class RelatorioExecutivoRepository : IRelatorioExecutivoRepository
{
    private readonly DapperContext _context; public RelatorioExecutivoRepository(DapperContext context) => _context = context;
    public async Task<RelatorioExecutivoDashboardDto> ObterAsync(long tenantId, RelatorioExecutivoFiltro filtro, CancellationToken ct)
    {
        const string sql = @"select coalesce(sum(valor_previsto),0) Previsto,coalesce(sum(valor_atualizado),0) Atualizado,coalesce(sum(valor_empenhado),0) Empenhado,coalesce(sum(valor_liquidado),0) Liquidado,coalesce(sum(valor_pago),0) Pago,coalesce(sum(saldo),0) Saldo,(select count(1) from sigov.financeiro_integracao_interna i where i.tenant_id=@TenantId and i.is_deleted=false and i.status='PENDENTE') IntegracoesPendentes from sigov.financeiro_dotacao d where d.tenant_id=@TenantId and d.is_deleted=false and (@EntidadeId is null or d.entidade_id=@EntidadeId) and (@ExercicioId is null or d.exercicio_id=@ExercicioId);
select coalesce(sum(valor_total),0) Lancado,coalesce(sum(valor_total-saldo),0) Arrecadado,coalesce(sum(saldo) filter(where status='EM_ABERTO'),0) EmAberto,coalesce(sum(saldo) filter(where status='PARCELADO'),0) Parcelado,(select coalesce(sum(saldo),0) from sigov.tributario_divida_ativa d where d.tenant_id=@TenantId and d.is_deleted=false and d.status in ('INSCRITO_DIVIDA','SUSPENSO')) DividaAtiva,coalesce(sum(valor_total) filter(where status='BAIXADO'),0) Baixado from sigov.tributario_lancamento l where l.tenant_id=@TenantId and l.is_deleted=false and (@EntidadeId is null or l.entidade_id=@EntidadeId) and (@Inicio is null or l.competencia>=@Inicio) and (@Fim is null or l.competencia<=@Fim);";
        using var connection = _context.CreateConnection();
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, filtro.EntidadeId, filtro.ExercicioId, filtro.Inicio, filtro.Fim }, cancellationToken: ct)).ConfigureAwait(false);
        var financeiro = await grid.ReadSingleAsync<RelatorioExecutivoFinanceiroDto>().ConfigureAwait(false);
        var tributario = await grid.ReadSingleAsync<RelatorioExecutivoTributarioDto>().ConfigureAwait(false);
        var rh = new RelatorioExecutivoRhFolhaDto(0, 0, 0, 0, 0); var educacao = new RelatorioExecutivoEducacaoDto(0, 0, 0, 0);
        var widgets = new[] { new RelatorioExecutivoWidgetDto("financeiro", "Execução paga", financeiro.Pago.ToString("C"), "Atual", "/RelatoriosExecutivos/Financeiro"), new RelatorioExecutivoWidgetDto("tributario", "Arrecadação", tributario.Arrecadado.ToString("C"), "Atual", "/RelatoriosExecutivos/Tributario"), new RelatorioExecutivoWidgetDto("tributario", "Dívida ativa", tributario.DividaAtiva.ToString("C"), "Atenção", "/Tributario/DividaAtiva") };
        return new RelatorioExecutivoDashboardDto(financeiro, tributario, rh, educacao, widgets, DateTimeOffset.UtcNow, "Indicadores exclusivamente agregados; documentos, salários individuais e dados de menores não são expostos.");
    }
    public async Task<long> SalvarFiltroAsync(long tenantId, long? usuarioId, RelatorioExecutivoFiltroSalvoRequest request, string correlationId, CancellationToken ct)
    { const string sql = "insert into sigov.relatorio_executivo_filtro_salvo(tenant_id,modulo,nome,tipo,filtros,status,correlation_id,created_by) values(@TenantId,@Modulo,@Nome,'FILTRO',cast(@Filtros as jsonb),'ATIVO',@CorrelationId,@UsuarioId) returning id;"; using var c = _context.CreateConnection(); return await c.ExecuteScalarAsync<long>(new CommandDefinition(sql,new{TenantId=tenantId,request.Modulo,request.Nome,Filtros=System.Text.Json.JsonSerializer.Serialize(request.Filtro),CorrelationId=correlationId,UsuarioId=usuarioId},cancellationToken:ct)).ConfigureAwait(false); }
    public async Task<IReadOnlyCollection<object>> ListarFiltrosAsync(long tenantId, long? usuarioId, CancellationToken ct)
    { const string sql = "select id,nome,modulo,filtros,status,created_at from sigov.relatorio_executivo_filtro_salvo where tenant_id=@TenantId and created_by=@UsuarioId and is_deleted=false order by created_at desc;"; using var c=_context.CreateConnection(); return (await c.QueryAsync<object>(new CommandDefinition(sql,new{TenantId=tenantId,UsuarioId=usuarioId},cancellationToken:ct)).ConfigureAwait(false)).AsList(); }
}
