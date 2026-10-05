using System.Text.Json;
using Dapper;
using Npgsql;
using Sigov.Application.Common;
using Sigov.Application.Financeiro;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Financeiro;

// ===========================================================================
// Helper de invariantes financeiras (fórmulas e derivação de status)
// ===========================================================================
public static class FinanceiroInvariantes
{
    /// <summary>Saldo a liquidar = valor_total - valor_anulado - valor_liquidado</summary>
    public static decimal SaldoALiquidar(decimal valorTotal, decimal valorAnulado, decimal valorLiquidado)
        => valorTotal - valorAnulado - valorLiquidado;

    /// <summary>Saldo a pagar = valor_liquidado - valor_pago</summary>
    public static decimal SaldoAPagar(decimal valorLiquidado, decimal valorPago)
        => valorLiquidado - valorPago;

    /// <summary>Saldo a arrecadar = valor - arrecadado</summary>
    public static decimal SaldoaArrecadar(decimal valor, decimal arrecadado)
        => valor - arrecadado;

    /// <summary>Derivação de status do empenho (prioridade fixa).</summary>
    public static string DerivarStatusEmpenho(decimal valorTotal, decimal valorAnulado, decimal valorLiquidado, decimal valorPago)
    {
        if (valorAnulado >= valorTotal) return "ANULADO";
        if (valorPago > 0 && valorPago >= valorLiquidado) return "PAGO_TOTAL";
        if (valorPago > 0 && valorPago < valorLiquidado) return "PAGO_PARCIAL";
        var efetivo = valorTotal - valorAnulado;
        if (valorLiquidado == efetivo) return "LIQUIDADO_TOTAL";
        if (valorLiquidado > 0) return "LIQUIDADO_PARCIAL";
        return "EMITIDO";
    }

    /// <summary>Derivação de status do lançamento de receita.</summary>
    public static string DerivarStatusLancamento(decimal valor, decimal arrecadado)
    {
        if (arrecadado >= valor && valor > 0) return "ARRECADADA";
        if (arrecadado > 0) return "PARCIALMENTE_ARRECADADA";
        return "LANCADA";
    }

    /// <summary>Arredondamento monetário canônico (2 casas, meio para fora). Centraliza fórmulas derivadas.</summary>
    public static decimal Money(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Inconsistência de saldo explícita (substitui greatest(0,...) que mascarava divergência).</summary>
    public const string Inconsistencia = "Inconsistência financeira: o saldo registrado é insuficiente para esta operação. Execute a conferência para detalhar a divergência.";

    /// <summary>Conflito de concorrência (deadlock/serialization) — HTTP 409 via prefixo, sem retry cego.</summary>
    public const string Concorrencia = "409: Conflito de concorrência em operação paralela sobre os mesmos documentos. Tente novamente.";
}

// ===========================================================================
// Guard de contexto para escrita (entidade ativa + exercício ativo no tenant)
// ===========================================================================
public static class FinanceiroContextoEscrever
{
    public static async Task GuardarAsync(NpgsqlConnection cn,NpgsqlTransaction nx,long t,long e,long x,CancellationToken ct)
    {
        var ctx=await cn.QueryFirstAsync<(int Entidade,int Exercicio)>(new CommandDefinition(
            @"select (select count(1) from sigov.entidade where id=@e and tenant_id=@t and ativo=true and is_deleted=false) as Entidade,
                     (select count(1) from sigov.exercicio where id=@x and entidade_id=@e and tenant_id=@t and ativo=true and is_deleted=false) as Exercicio",
            new{t,e,x},nx,cancellationToken:ct)).ConfigureAwait(false);
        if(ctx.Entidade!=1) throw new InvalidOperationException("Contexto inválido: entidade ausente ou inativa. Selecione uma entidade ativa e tente novamente.");
        if(ctx.Exercicio!=1) throw new InvalidOperationException("Contexto inválido: exercício encerrado ou ausente. Selecione um exercício ativo e tente novamente.");
    }
}

// ===========================================================================
// CRUD base genérico
// ===========================================================================
public abstract class CrudFinanceiroRepository<TCreate, TUpdate, TFiltro, TResponse> : BaseRepository
{
    private readonly DapperContext _context;
    private readonly string _table;
    private readonly string _select;
    protected CrudFinanceiroRepository(DapperContext context, string table, string select) { _context = context; _table = table; _select = select; }
    public async Task<PagedResult<TResponse>> ListarAsync(long tenantId, long entidadeId, long exercicioId, TFiltro filtro, CancellationToken ct)
    {
        var (page, pageSize) = Page(filtro);
        var safePage = Math.Max(1, page); var safeSize = Math.Clamp(pageSize, 1, 100); var offset = (safePage - 1) * safeSize;
        var sql = $"select {_select} from {_table} where tenant_id=@TenantId and entidade_id=@EntidadeId and exercicio_id=@ExercicioId and is_deleted=false order by id desc limit @Limit offset @Offset; select count(1) from {_table} where tenant_id=@TenantId and entidade_id=@EntidadeId and exercicio_id=@ExercicioId and is_deleted=false;";
        using var cn = _context.CreateConnection();
        using var grid = await cn.QueryMultipleAsync(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Limit = safeSize, Offset = offset }, ct)).ConfigureAwait(false);
        var rows = (await grid.ReadAsync<TResponse>().ConfigureAwait(false)).AsList();
        var total = await grid.ReadSingleAsync<long>().ConfigureAwait(false);
        return new PagedResult<TResponse>(rows, safePage, safeSize, total);
    }
    public async Task<TResponse?> ObterAsync(long tenantId, long entidadeId, long exercicioId, long id, CancellationToken ct)
    {
        var sql = $"select {_select} from {_table} where tenant_id=@TenantId and entidade_id=@EntidadeId and exercicio_id=@ExercicioId and id=@Id and is_deleted=false;";
        using var cn = _context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<TResponse>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Id = id }, ct)).ConfigureAwait(false);
    }
    public abstract Task<long> CriarAsync(long tenantId, long entidadeId, long exercicioId, TCreate request, long? usuarioId, CancellationToken ct);
    public abstract Task AtualizarAsync(long tenantId, long entidadeId, long exercicioId, long id, TUpdate request, long? usuarioId, CancellationToken ct);
    public async Task ExcluirAsync(long tenantId, long entidadeId, long exercicioId, long id, long? usuarioId, CancellationToken ct)
    { using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command($"update {_table} set is_deleted=true, ativo=false, deleted_at=now(), deleted_by=@UsuarioId where tenant_id=@TenantId and entidade_id=@EntidadeId and exercicio_id=@ExercicioId and id=@Id and is_deleted=false;", new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    protected DapperContext Context => _context;
    private static (int Page, int PageSize) Page(TFiltro filtro)
    { var type = typeof(TFiltro); return ((int?)type.GetProperty("Page")?.GetValue(filtro) ?? 1, (int?)type.GetProperty("PageSize")?.GetValue(filtro) ?? 20); }
}

// ===========================================================================
// Repositórios CRUD (catálogos)
// ===========================================================================
public sealed class PlanoContasRepository : CrudFinanceiroRepository<PlanoContasCreateRequest, PlanoContasUpdateRequest, PlanoContasFiltro, PlanoContasResponse>, IPlanoContasRepository
{
    public PlanoContasRepository(DapperContext c) : base(c, "sigov.plano_contas", "id, codigo, nome, tipo_conta as TipoConta, nivel, conta_pai_id as ContaPaiId, natureza_saldo as NaturezaSaldo, aceita_lancamento as AceitaLancamento, ativo") { }
    public override async Task<long> CriarAsync(long t, long e, long x, PlanoContasCreateRequest r, long? u, CancellationToken ct) { const string sql = "insert into sigov.plano_contas (tenant_id,entidade_id,exercicio_id,codigo,nome,tipo_conta,nivel,conta_pai_id,natureza_saldo,aceita_lancamento,created_by) values (@t,@e,@x,@Codigo,@Nome,@TipoConta,@Nivel,@ContaPaiId,@NaturezaSaldo,@AceitaLancamento,@u) returning id;"; using var cn = Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { t, e, x, r.Codigo, r.Nome, r.TipoConta, r.Nivel, r.ContaPaiId, r.NaturezaSaldo, r.AceitaLancamento, u }, ct)).ConfigureAwait(false); }
    public override async Task AtualizarAsync(long t, long e, long x, long id, PlanoContasUpdateRequest r, long? u, CancellationToken ct) { const string sql = "update sigov.plano_contas set codigo=@Codigo,nome=@Nome,tipo_conta=@TipoConta,nivel=@Nivel,conta_pai_id=@ContaPaiId,natureza_saldo=@NaturezaSaldo,aceita_lancamento=@AceitaLancamento,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn = Context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { t, e, x, id, r.Codigo, r.Nome, r.TipoConta, r.Nivel, r.ContaPaiId, r.NaturezaSaldo, r.AceitaLancamento, r.Ativo, u }, ct)).ConfigureAwait(false); }
}
public sealed class FonteRecursoRepository : CrudFinanceiroRepository<FonteRecursoCreateRequest, FonteRecursoUpdateRequest, FonteRecursoFiltro, FonteRecursoResponse>, IFonteRecursoRepository { public FonteRecursoRepository(DapperContext c) : base(c, "sigov.fonte_recurso", "id,codigo,nome,descricao,ativo") { } public override async Task<long> CriarAsync(long t,long e,long x,FonteRecursoCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.fonte_recurso (tenant_id,entidade_id,exercicio_id,codigo,nome,descricao,created_by) values (@t,@e,@x,@Codigo,@Nome,@Descricao,@u) returning id;"; using var cn=Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.Codigo,r.Nome,r.Descricao,u},ct)).ConfigureAwait(false);} public override async Task AtualizarAsync(long t,long e,long x,long id,FonteRecursoUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.fonte_recurso set codigo=@Codigo,nome=@Nome,descricao=@Descricao,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=Context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.Codigo,r.Nome,r.Descricao,r.Ativo,u},ct)).ConfigureAwait(false);} }
public sealed class ProgramaRepository : CrudFinanceiroRepository<ProgramaCreateRequest, ProgramaUpdateRequest, ProgramaFiltro, ProgramaResponse>, IProgramaRepository { public ProgramaRepository(DapperContext c) : base(c, "sigov.programa", "id,codigo,nome,objetivo,ativo") { } public override async Task<long> CriarAsync(long t,long e,long x,ProgramaCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.programa (tenant_id,entidade_id,exercicio_id,codigo,nome,objetivo,created_by) values (@t,@e,@x,@Codigo,@Nome,@Objetivo,@u) returning id;"; using var cn=Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.Codigo,r.Nome,r.Objetivo,u},ct)).ConfigureAwait(false);} public override async Task AtualizarAsync(long t,long e,long x,long id,ProgramaUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.programa set codigo=@Codigo,nome=@Nome,objetivo=@Objetivo,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=Context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.Codigo,r.Nome,r.Objetivo,r.Ativo,u},ct)).ConfigureAwait(false);} }
public sealed class AcaoRepository : CrudFinanceiroRepository<AcaoCreateRequest, AcaoUpdateRequest, AcaoFiltro, AcaoResponse>, IAcaoRepository { public AcaoRepository(DapperContext c) : base(c, "sigov.acao", "id,programa_id as ProgramaId,codigo,nome,tipo_acao as TipoAcao,ativo") { } public override async Task<long> CriarAsync(long t,long e,long x,AcaoCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.acao (tenant_id,entidade_id,exercicio_id,programa_id,codigo,nome,tipo_acao,created_by) values (@t,@e,@x,@ProgramaId,@Codigo,@Nome,@TipoAcao,@u) returning id;"; using var cn=Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.ProgramaId,r.Codigo,r.Nome,r.TipoAcao,u},ct)).ConfigureAwait(false);} public override async Task AtualizarAsync(long t,long e,long x,long id,AcaoUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.acao set programa_id=@ProgramaId,codigo=@Codigo,nome=@Nome,tipo_acao=@TipoAcao,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=Context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.ProgramaId,r.Codigo,r.Nome,r.TipoAcao,r.Ativo,u},ct)).ConfigureAwait(false);} }
public sealed class NaturezaReceitaRepository : CrudFinanceiroRepository<NaturezaReceitaCreateRequest, NaturezaReceitaUpdateRequest, NaturezaReceitaFiltro, NaturezaReceitaResponse>, INaturezaReceitaRepository { public NaturezaReceitaRepository(DapperContext c) : base(c, "sigov.natureza_receita", "id,codigo,nome,categoria,origem,especie,ativo") { } public override async Task<long> CriarAsync(long t,long e,long x,NaturezaReceitaCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.natureza_receita (tenant_id,entidade_id,exercicio_id,codigo,nome,categoria,origem,especie,created_by) values (@t,@e,@x,@Codigo,@Nome,@Categoria,@Origem,@Especie,@u) returning id;"; using var cn=Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.Codigo,r.Nome,r.Categoria,r.Origem,r.Especie,u},ct)).ConfigureAwait(false);} public override async Task AtualizarAsync(long t,long e,long x,long id,NaturezaReceitaUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.natureza_receita set codigo=@Codigo,nome=@Nome,categoria=@Categoria,origem=@Origem,especie=@Especie,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=Context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.Codigo,r.Nome,r.Categoria,r.Origem,r.Especie,r.Ativo,u},ct)).ConfigureAwait(false);} }
public sealed class NaturezaDespesaRepository : CrudFinanceiroRepository<NaturezaDespesaCreateRequest, NaturezaDespesaUpdateRequest, NaturezaDespesaFiltro, NaturezaDespesaResponse>, INaturezaDespesaRepository { public NaturezaDespesaRepository(DapperContext c) : base(c, "sigov.natureza_despesa", "id,codigo,nome,categoria,grupo,modalidade,elemento,ativo") { } public override async Task<long> CriarAsync(long t,long e,long x,NaturezaDespesaCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.natureza_despesa (tenant_id,entidade_id,exercicio_id,codigo,nome,categoria,grupo,modalidade,elemento,created_by) values (@t,@e,@x,@Codigo,@Nome,@Categoria,@Grupo,@Modalidade,@Elemento,@u) returning id;"; using var cn=Context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.Codigo,r.Nome,r.Categoria,r.Grupo,r.Modalidade,r.Elemento,u},ct)).ConfigureAwait(false);} public override async Task AtualizarAsync(long t,long e,long x,long id,NaturezaDespesaUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.natureza_despesa set codigo=@Codigo,nome=@Nome,categoria=@Categoria,grupo=@Grupo,modalidade=@Modalidade,elemento=@Elemento,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=Context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.Codigo,r.Nome,r.Categoria,r.Grupo,r.Modalidade,r.Elemento,r.Ativo,u},ct)).ConfigureAwait(false);} }

// ===========================================================================
// Sequencial
// ===========================================================================
public sealed class FinanceiroSequencialRepository : BaseRepository, IFinanceiroSequencialService
{
    private readonly DapperContext _context; public FinanceiroSequencialRepository(DapperContext context) => _context = context;
    public async Task<string> ProximoAsync(long tenantId, long entidadeId, long exercicioId, int ano, string escopo, string prefixo, CancellationToken ct)
    { const string sql = "insert into sigov.financeiro_sequencial (tenant_id,entidade_id,exercicio_id,ano,escopo,ultimo_numero) values (@TenantId,@EntidadeId,@ExercicioId,@Ano,@Escopo,1) on conflict (tenant_id,entidade_id,exercicio_id,ano,escopo) do update set ultimo_numero=sigov.financeiro_sequencial.ultimo_numero+1, updated_at=now() returning ultimo_numero;"; using var cn = _context.CreateConnection(); var n = await cn.ExecuteScalarAsync<int>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Ano = ano, Escopo = escopo }, ct)).ConfigureAwait(false); return $"{prefixo}-{ano}-{n:000000}"; }
}

// ===========================================================================
// Orçamento
// ===========================================================================
public sealed class OrcamentoRepository : BaseRepository, IOrcamentoRepository
{
    private readonly DapperContext _context; public OrcamentoRepository(DapperContext c) => _context = c;
    public async Task<PagedResult<OrcamentoDespesaResponse>> ListarDespesasAsync(long t,long e,long x,OrcamentoDespesaFiltro f,CancellationToken ct){var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);var where="tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false"; if(f.ProgramaId.HasValue) where+=" and programa_id=@ProgramaId"; if(f.AcaoId.HasValue) where+=" and acao_id=@AcaoId"; if(f.NaturezaDespesaId.HasValue) where+=" and natureza_despesa_id=@NaturezaDespesaId"; if(f.FonteRecursoId.HasValue) where+=" and fonte_recurso_id=@FonteRecursoId"; var sql=$"select id,orgao_unidade_orcamentaria_id as OrgaoUnidadeOrcamentariaId,programa_id as ProgramaId,acao_id as AcaoId,natureza_despesa_id as NaturezaDespesaId,fonte_recurso_id as FonteRecursoId,dotacao_inicial as DotacaoInicial,suplementacoes,reducoes,reservado,empenhado,liquidado,pago,(dotacao_inicial+suplementacoes-reducoes-reservado-empenhado) as SaldoDisponivel,ativo from sigov.orcamento_despesa where {where} order by id desc limit @size offset @offset; select count(1) from sigov.orcamento_despesa where {where};"; using var cn=_context.CreateConnection(); using var g=await cn.QueryMultipleAsync(Command(sql,new{t,e,x,f.ProgramaId,f.AcaoId,f.NaturezaDespesaId,f.FonteRecursoId,size,offset=(page-1)*size},ct)).ConfigureAwait(false); return new PagedResult<OrcamentoDespesaResponse>((await g.ReadAsync<OrcamentoDespesaResponse>().ConfigureAwait(false)).AsList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));}
    public async Task<OrcamentoDespesaResponse?> ObterDespesaAsync(long t,long e,long x,long id,CancellationToken ct){const string sql="select id,orgao_unidade_orcamentaria_id as OrgaoUnidadeOrcamentariaId,programa_id as ProgramaId,acao_id as AcaoId,natureza_despesa_id as NaturezaDespesaId,fonte_recurso_id as FonteRecursoId,dotacao_inicial as DotacaoInicial,suplementacoes,reducoes,reservado,empenhado,liquidado,pago,(dotacao_inicial+suplementacoes-reducoes-reservado-empenhado) as SaldoDisponivel,ativo from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=_context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<OrcamentoDespesaResponse>(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);}
    public async Task<long> CriarDespesaAsync(long t,long e,long x,OrcamentoDespesaCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.orcamento_despesa (tenant_id,entidade_id,exercicio_id,orgao_unidade_orcamentaria_id,programa_id,acao_id,natureza_despesa_id,fonte_recurso_id,dotacao_inicial,created_by) values (@t,@e,@x,@OrgaoUnidadeOrcamentariaId,@ProgramaId,@AcaoId,@NaturezaDespesaId,@FonteRecursoId,@DotacaoInicial,@u) returning id;"; using var cn=_context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.OrgaoUnidadeOrcamentariaId,r.ProgramaId,r.AcaoId,r.NaturezaDespesaId,r.FonteRecursoId,r.DotacaoInicial,u},ct)).ConfigureAwait(false);}
    public async Task AtualizarDespesaAsync(long t,long e,long x,long id,OrcamentoDespesaUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.orcamento_despesa set orgao_unidade_orcamentaria_id=@OrgaoUnidadeOrcamentariaId,programa_id=@ProgramaId,acao_id=@AcaoId,natureza_despesa_id=@NaturezaDespesaId,fonte_recurso_id=@FonteRecursoId,dotacao_inicial=@DotacaoInicial,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=_context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.OrgaoUnidadeOrcamentariaId,r.ProgramaId,r.AcaoId,r.NaturezaDespesaId,r.FonteRecursoId,r.DotacaoInicial,r.Ativo,u},ct)).ConfigureAwait(false);}
    public async Task MovimentarDespesaAsync(long t,long e,long x,long id,MovimentacaoOrcamentariaRequest r,long? u,CancellationToken ct)
    {
        // RESERVA/ESTORNO_RESERVA → coluna reservado; REDUCAO → reducoes; SUPLEMENTACAO → suplementacoes
        if(r.Valor<=0m) throw new InvalidOperationException("Valor da movimentação deve ser positivo.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);
            var tipo=r.TipoMovimentacao?.Trim().ToUpperInvariant() ?? "";
            string col,sinal;
            switch(tipo)
            {
                case "RESERVA": col="reservado";sinal="+";break;
                case "ESTORNO_RESERVA": col="reservado";sinal="-";break;
                case "REDUCAO": col="reducoes";sinal="+";break;
                case "SUPLEMENTACAO": col="suplementacoes";sinal="+";break;
                default: throw new InvalidOperationException("Tipo de movimentação inválido.");
            }
            // Lock do documento e limites por tipo (sem mascaramento de inconsistência)
            var od=await cn.QueryFirstOrDefaultAsync<(long Id,decimal SaldoDisponivel,decimal Reservado)>(new CommandDefinition(
                @"select id,(dotacao_inicial+suplementacoes-reducoes-reservado-empenhado) as SaldoDisponivel,reservado as Reservado from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(od.Id==0) throw new InvalidOperationException("Dotação orçamentária não encontrada.");
            switch(tipo)
            {
                case "RESERVA": if(r.Valor>od.SaldoDisponivel) throw new InvalidOperationException($"Saldo disponível insuficiente para reserva. Máximo: {od.SaldoDisponivel:F2}.");break;
                case "ESTORNO_RESERVA": if(r.Valor>od.Reservado) throw new InvalidOperationException($"Reserva insuficiente para estorno. Máximo: {od.Reservado:F2}.");break;
                case "REDUCAO": if(r.Valor>od.SaldoDisponivel) throw new InvalidOperationException($"Redução excede o saldo disponível autorizado. Máximo: {od.SaldoDisponivel:F2}.");break;
            }
            var sql=$@"update sigov.orcamento_despesa set {col}={col}{sinal}@Valor,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;
insert into sigov.orcamento_movimentacao (tenant_id,entidade_id,exercicio_id,orcamento_despesa_id,tipo_movimentacao,valor,historico,created_by) values (@t,@e,@x,@id,@TipoMovimentacao,@Valor,@Historico,@u);";
            await cn.ExecuteAsync(new CommandDefinition(sql,new{t,e,x,id,r.TipoMovimentacao,r.Valor,r.Historico,u},nx,cancellationToken:ct)).ConfigureAwait(false);
            nx.Commit();
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }
    public async Task<PagedResult<OrcamentoReceitaResponse>> ListarReceitasAsync(long t,long e,long x,OrcamentoReceitaFiltro f,CancellationToken ct){var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);var where="tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false"; if(f.NaturezaReceitaId.HasValue) where+=" and natureza_receita_id=@NaturezaReceitaId"; if(f.FonteRecursoId.HasValue) where+=" and fonte_recurso_id=@FonteRecursoId"; var sql=$"select id,natureza_receita_id as NaturezaReceitaId,fonte_recurso_id as FonteRecursoId,previsao_inicial as PrevisaoInicial,previsao_atualizada as PrevisaoAtualizada,lancado,arrecadado,ativo from sigov.orcamento_receita where {where} order by id desc limit @size offset @offset; select count(1) from sigov.orcamento_receita where {where};"; using var cn=_context.CreateConnection(); using var g=await cn.QueryMultipleAsync(Command(sql,new{t,e,x,f.NaturezaReceitaId,f.FonteRecursoId,size,offset=(page-1)*size},ct)).ConfigureAwait(false); return new PagedResult<OrcamentoReceitaResponse>((await g.ReadAsync<OrcamentoReceitaResponse>().ConfigureAwait(false)).AsList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));}
    public async Task<OrcamentoReceitaResponse?> ObterReceitaAsync(long t,long e,long x,long id,CancellationToken ct){const string sql="select id,natureza_receita_id as NaturezaReceitaId,fonte_recurso_id as FonteRecursoId,previsao_inicial as PrevisaoInicial,previsao_atualizada as PrevisaoAtualizada,lancado,arrecadado,ativo from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=_context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<OrcamentoReceitaResponse>(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);}
    public async Task<long> CriarReceitaAsync(long t,long e,long x,OrcamentoReceitaCreateRequest r,long? u,CancellationToken ct){const string sql="insert into sigov.orcamento_receita (tenant_id,entidade_id,exercicio_id,natureza_receita_id,fonte_recurso_id,previsao_inicial,previsao_atualizada,created_by) values (@t,@e,@x,@NaturezaReceitaId,@FonteRecursoId,@PrevisaoInicial,@PrevisaoInicial,@u) returning id;"; using var cn=_context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql,new{t,e,x,r.NaturezaReceitaId,r.FonteRecursoId,r.PrevisaoInicial,u},ct)).ConfigureAwait(false);}
    public async Task AtualizarReceitaAsync(long t,long e,long x,long id,OrcamentoReceitaUpdateRequest r,long? u,CancellationToken ct){const string sql="update sigov.orcamento_receita set natureza_receita_id=@NaturezaReceitaId,fonte_recurso_id=@FonteRecursoId,previsao_inicial=@PrevisaoInicial,previsao_atualizada=@PrevisaoAtualizada,ativo=@Ativo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;"; using var cn=_context.CreateConnection(); await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.NaturezaReceitaId,r.FonteRecursoId,r.PrevisaoInicial,r.PrevisaoAtualizada,r.Ativo,u},ct)).ConfigureAwait(false);}
}

// ===========================================================================
// Empenho
// ===========================================================================
public sealed class EmpenhoRepository : BaseRepository, IEmpenhoRepository
{
    private readonly DapperContext _context;
    private readonly IFinanceiroSequencialService _seq;
    public EmpenhoRepository(DapperContext c,IFinanceiroSequencialService seq) { _context=c; _seq=seq; }

    public async Task<PagedResult<EmpenhoResumoResponse>> ListarAsync(long t,long e,long x,EmpenhoFiltro f,CancellationToken ct)
    {
        var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);
        var where="emp.tenant_id=@t and emp.entidade_id=@e and emp.exercicio_id=@x and emp.is_deleted=false";
        var pars = new DynamicParameters(); pars.Add("t",t); pars.Add("e",e); pars.Add("x",x);
        if(!string.IsNullOrWhiteSpace(f.Numero)){where+=" and emp.numero like @Numero"; pars.Add("Numero",$"%{f.Numero}%");}
        if(!string.IsNullOrWhiteSpace(f.Fornecedor)){where+=" and p.nome ilike @Fornecedor"; pars.Add("Fornecedor",$"%{f.Fornecedor}%");}
        if(!string.IsNullOrWhiteSpace(f.Status)){where+=" and emp.status=@Status"; pars.Add("Status",f.Status);}
        if(f.Inicio.HasValue){where+=" and emp.data_empenho>=@Inicio"; pars.Add("Inicio",f.Inicio.Value);}
        if(f.Fim.HasValue){where+=" and emp.data_empenho<=@Fim"; pars.Add("Fim",f.Fim.Value);}
        if(f.NaturezaDespesaId.HasValue){where+=" and od.natureza_despesa_id=@NaturezaDespesaId"; pars.Add("NaturezaDespesaId",f.NaturezaDespesaId.Value);}
        if(f.FonteRecursoId.HasValue){where+=" and od.fonte_recurso_id=@FonteRecursoId"; pars.Add("FonteRecursoId",f.FonteRecursoId.Value);}
        if(f.ProgramaId.HasValue){where+=" and od.programa_id=@ProgramaId"; pars.Add("ProgramaId",f.ProgramaId.Value);}
        if(f.AcaoId.HasValue){where+=" and od.acao_id=@AcaoId"; pars.Add("AcaoId",f.AcaoId.Value);}
        const string select="emp.id, emp.numero, emp.data_empenho as DataEmpenho, coalesce(p.nome,'Fornecedor protegido') as Fornecedor, coalesce(nd.nome,'') as Natureza, coalesce(fr.nome,'') as Fonte, emp.valor_total as ValorTotal, emp.valor_liquidado as ValorLiquidado, emp.valor_pago as ValorPago, (emp.valor_total-emp.valor_anulado-emp.valor_liquidado) as Saldo, emp.status";
        var sql=$"select {select} from sigov.empenho emp join sigov.orcamento_despesa od on od.id=emp.orcamento_despesa_id and od.tenant_id=@t left join sigov.pessoa p on p.id=emp.fornecedor_pessoa_id left join sigov.natureza_despesa nd on nd.id=od.natureza_despesa_id left join sigov.fonte_recurso fr on fr.id=od.fonte_recurso_id where {where} order by emp.id desc limit @size offset @offset; select count(1) from sigov.empenho emp join sigov.orcamento_despesa od on od.id=emp.orcamento_despesa_id and od.tenant_id=@t left join sigov.pessoa p on p.id=emp.fornecedor_pessoa_id where {where};";
        using var cn=_context.CreateConnection();
        pars.Add("size",size); pars.Add("offset",(page-1)*size);
        using var g=await cn.QueryMultipleAsync(Command(sql,pars,ct)).ConfigureAwait(false);
        return new PagedResult<EmpenhoResumoResponse>((await g.ReadAsync<EmpenhoResumoResponse>().ConfigureAwait(false)).AsList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));
    }

    public async Task<EmpenhoDetalheResponse?> ObterAsync(long t,long e,long x,long id,CancellationToken ct)
    {
        const string sql=@"select emp.id, emp.numero, emp.data_empenho as DataEmpenho, emp.orcamento_despesa_id as OrcamentoDespesaId, emp.fornecedor_pessoa_id as FornecedorPessoaId, coalesce(p.nome,'Fornecedor protegido') as Fornecedor, emp.historico, emp.tipo_empenho as TipoEmpenho, emp.valor_total as ValorTotal, emp.valor_anulado as ValorAnulado, emp.valor_liquidado as ValorLiquidado, emp.valor_pago as ValorPago, emp.status, emp.motivo from sigov.empenho emp left join sigov.pessoa p on p.id=emp.fornecedor_pessoa_id where emp.tenant_id=@t and emp.entidade_id=@e and emp.exercicio_id=@x and emp.id=@id and emp.is_deleted=false;
select id,descricao,quantidade,valor_unitario as ValorUnitario,valor_total as ValorTotal from sigov.empenho_item where tenant_id=@t and entidade_id=@e and exercicio_id=@x and empenho_id=@id and is_deleted=false order by id;";
        using var cn=_context.CreateConnection();
        using var g=await cn.QueryMultipleAsync(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);
        var emp=await g.ReadSingleOrDefaultAsync<EmpenhoDetalheRow>().ConfigureAwait(false);
        if(emp is null)return null;
        var itens=(await g.ReadAsync<EmpenhoItemResponse>().ConfigureAwait(false)).AsList();
        return new EmpenhoDetalheResponse(emp.Id,emp.Numero,emp.DataEmpenho,emp.OrcamentoDespesaId,emp.FornecedorPessoaId,emp.Fornecedor,emp.Historico,emp.TipoEmpenho,emp.ValorTotal,emp.ValorAnulado,emp.ValorLiquidado,emp.ValorPago,emp.Status,itens);
    }

    public async Task<FinanceiroResultadoComando> CriarAsync(long t,long e,long x,int ano,EmpenhoCreateRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        foreach(var (item,pos) in r.Itens.Select((i,p)=>(item:i,pos:p+1)))
        {
            if(string.IsNullOrWhiteSpace(item.Descricao)) throw new ArgumentException($"Item {pos}: descrição é obrigatória.");
            if(FinanceiroInvariantes.Money(item.Quantidade)!=item.Quantidade) throw new ArgumentException($"Item {pos}: quantidade deve ter no máximo 2 casas decimais.");
            if(item.Quantidade<=0m) throw new ArgumentException($"Item {pos}: quantidade deve ser maior que zero.");
            if(FinanceiroInvariantes.Money(item.ValorUnitario)!=item.ValorUnitario) throw new ArgumentException($"Item {pos}: valor unitário deve ter no máximo 2 casas decimais.");
            if(item.ValorUnitario<=0m) throw new ArgumentException($"Item {pos}: valor unitário deve ser maior que zero.");
        }
        var valor=r.Itens.Sum(i=>FinanceiroInvariantes.Money(i.Quantidade*i.ValorUnitario));
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            if(r.FornecedorPessoaId is >0)
            {
                var fornecedorOk=await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"select count(1) from sigov.pessoa where id=@fid and tenant_id=@t and ativo=true and is_deleted=false and (entidade_id is null or entidade_id=@e) and (exercicio_id is null or exercicio_id=@x)",
                    new{t,e,x,fid=r.FornecedorPessoaId},nx,cancellationToken:ct)).ConfigureAwait(false);
                if(fornecedorOk==0) throw new InvalidOperationException("Fornecedor não localizado ou inativo no contexto selecionado.");
            }

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"empenho.criar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return new FinanceiroResultadoComando(docReplay,true);
            }

            // Lock orcamento_despesa e validar saldo/catálogos vinculados
            var orcamento = await cn.QueryFirstOrDefaultAsync<(long Id,decimal Saldo,long NaturezaId,long FonteId)>(new CommandDefinition(
                @"select id,(dotacao_inicial+suplementacoes-reducoes-reservado-empenhado) as Saldo,natureza_despesa_id as NaturezaId,fonte_recurso_id as FonteId from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@OrcamentoDespesaId and is_deleted=false for update",
                new{t,e,x,r.OrcamentoDespesaId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(orcamento.Id==0) throw new InvalidOperationException("Dotação orçamentária não encontrada.");
            if(valor > orcamento.Saldo) throw new InvalidOperationException($"Saldo disponível insuficiente. Disponível: {orcamento.Saldo:F2}, Solicitado: {valor:F2}.");
            var catalogos=await cn.QueryFirstAsync<(int Natureza,int Fonte)>(new CommandDefinition(
                @"select (select count(1) from sigov.natureza_despesa where id=@nid and tenant_id=@t and ativo=true and is_deleted=false) as Natureza,
                         (select count(1) from sigov.fonte_recurso where id=@fid and tenant_id=@t and ativo=true and is_deleted=false) as Fonte",
                new{t,nid=orcamento.NaturezaId,fid=orcamento.FonteId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(catalogos.Natureza==0) throw new InvalidOperationException("Natureza da despesa vinculada à dotação não localizada ou inativa.");
            if(catalogos.Fonte==0) throw new InvalidOperationException("Fonte de recurso vinculada à dotação não localizada ou inativa.");

            // Sequencial LAZY: gerado dentro da transação, após o check de replay
            var numero=await _seq.ProximoAsync(t,e,x,ano,"empenho","EMP",ct).ConfigureAwait(false);

            // Update acumulador
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set empenhado=empenhado+@valor,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@OrcamentoDespesaId",
                new{t,e,x,r.OrcamentoDespesaId,valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Insert empenho
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.empenho (tenant_id,entidade_id,exercicio_id,orcamento_despesa_id,numero,ano,data_empenho,fornecedor_pessoa_id,historico,tipo_empenho,valor_total,status,metadados,created_by) values (@t,@e,@x,@OrcamentoDespesaId,@numero,@ano,@DataEmpenho,@FornecedorPessoaId,@Historico,@TipoEmpenho,@valor,'EMITIDO',jsonb_build_object('observacoes',@Observacoes),@u) returning id",
                new{t,e,x,r.OrcamentoDespesaId,numero,ano,r.DataEmpenho,r.FornecedorPessoaId,r.Historico,r.TipoEmpenho,Observacoes=r.Observacoes,valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Insert itens
            foreach(var item in r.Itens)
            {
                var itemTotal=FinanceiroInvariantes.Money(item.Quantidade*item.ValorUnitario);
                await cn.ExecuteAsync(new CommandDefinition(
                    @"insert into sigov.empenho_item (tenant_id,entidade_id,exercicio_id,empenho_id,descricao,quantidade,valor_unitario,valor_total,created_by) values (@t,@e,@x,@id,@Descricao,@Quantidade,@ValorUnitario,@ValorTotal,@u)",
                    new{t,e,x,id,item.Descricao,item.Quantidade,item.ValorUnitario,ValorTotal=itemTotal,u},nx,cancellationToken:ct)).ConfigureAwait(false);
            }

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="EmpenhoEmitido",payload=JsonSerializer.Serialize(new{id,numero,valor}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"empenho.criar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return new FinanceiroResultadoComando(id,false);
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task AtualizarAsync(long t,long e,long x,long id,EmpenhoUpdateRequest r,long? u,CancellationToken ct)
    {
        const string sql=@"update sigov.empenho set data_empenho=@DataEmpenho,historico=@Historico,tipo_empenho=@TipoEmpenho,metadados=jsonb_build_object('observacoes',@Observacoes),updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false and status <> 'ANULADO';";
        using var cn=_context.CreateConnection();
        var rows=await cn.ExecuteAsync(Command(sql,new{t,e,x,id,r.DataEmpenho,r.Historico,r.TipoEmpenho,r.Observacoes,u},ct)).ConfigureAwait(false);
        if(rows==0) throw new InvalidOperationException("Empenho não encontrado ou já anulado.");
    }

    public async Task<bool> AnularAsync(long t,long e,long x,long id,AnularEmpenhoRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.Motivo)) throw new InvalidOperationException("Motivo é obrigatório para anulação de empenho.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"empenho.anular",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},id,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return true;
            }

            // Snapshot sem lock para localizar o pai
            var snapOd=await cn.ExecuteScalarAsync<long?>(new CommandDefinition(
                @"select orcamento_despesa_id from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapOd is null) throw new InvalidOperationException("Empengo não encontrado.");

            // Lock na ordem pai -> filho: orcamento_despesa antes de empenho
            var od=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Empenhado)>(new CommandDefinition(
                @"select id,empenhado as Empenhado from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=snapOd.Value},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(od.Id==0) throw new InvalidOperationException("Dotação orçamentária não encontrada.");

            // Lock empenho (revalidação sob lock)
            var emp=await cn.QueryFirstOrDefaultAsync<EmpenhoRow>(new CommandDefinition(
                @"select id as Id,valor_total as ValorTotal,valor_anulado as ValorAnulado,valor_liquidado as ValorLiquidado,valor_pago as ValorPago,status as Status,orcamento_despesa_id as OrcamentoDespesaId from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(emp is null) throw new InvalidOperationException("Empenho não encontrado.");
            if(emp.Status=="ANULADO") throw new InvalidOperationException("409: Empenho já anulado.");

            var maxAnulavel = FinanceiroInvariantes.SaldoALiquidar(emp.ValorTotal, emp.ValorAnulado, emp.ValorLiquidado);
            if(r.Valor <= 0) throw new InvalidOperationException("Valor de anulação deve ser positivo.");
            if(r.Valor > maxAnulavel) throw new InvalidOperationException($"Valor excede o saldo anulável. Máximo: {maxAnulavel:F2}.");

            var novoAnulado = emp.ValorAnulado + r.Valor;
            var novoStatus = FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal, novoAnulado, emp.ValorLiquidado, emp.ValorPago);

            // Update empenho
            var rows=await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.empenho set valor_anulado=@novoAnulado,status=@novoStatus,motivo=@Motivo,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id",
                new{t,e,x,id,novoAnulado,novoStatus,r.Motivo,u},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(rows==0) throw new InvalidOperationException("Conflito de concorrência ao anular empenho.");

            // Update orcamento_despesa (sem greatest(0,...): inconsistência fica explícita)
            if(od.Empenhado < r.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set empenhado=empenhado-@Valor,updated_at=now(),updated_by=@u where id=@orcamentoDespesaId and tenant_id=@t",
                new{t,Valor=r.Valor,orcamentoDespesaId=emp.OrcamentoDespesaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="EmpenhoAnulado",payload=JsonSerializer.Serialize(new{id,valor=r.Valor,motivo=r.Motivo,novoStatus}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"empenho.anular",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return false;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    private sealed record EmpenhoRow(long Id,decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago,string Status,long OrcamentoDespesaId);
    private sealed record EmpenhoDetalheRow(long Id,string Numero,DateOnly DataEmpenho,long OrcamentoDespesaId,long FornecedorPessoaId,string Fornecedor,string Historico,string TipoEmpenho,decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago,string Status,string? Motivo);
}

// ===========================================================================
// Liquidação
// ===========================================================================
public sealed class LiquidacaoRepository : BaseRepository, ILiquidacaoRepository
{
    private readonly DapperContext _context;
    private readonly IFinanceiroSequencialService _seq;
    public LiquidacaoRepository(DapperContext c,IFinanceiroSequencialService seq) { _context=c; _seq=seq; }

    public async Task<PagedResult<LiquidacaoResponse>> ListarAsync(long t,long e,long x,LiquidacaoFiltro f,CancellationToken ct)
    {
        var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);
        var where="tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false";
        var pars=new DynamicParameters(); pars.Add("t",t); pars.Add("e",e); pars.Add("x",x);
        if(!string.IsNullOrWhiteSpace(f.Numero)){where+=" and numero like @Numero"; pars.Add("Numero",$"%{f.Numero}%");}
        if(f.EmpenhoId.HasValue){where+=" and empenho_id=@EmpenhoId"; pars.Add("EmpenhoId",f.EmpenhoId.Value);}
        if(!string.IsNullOrWhiteSpace(f.Status)){where+=" and status=@Status"; pars.Add("Status",f.Status);}
        var sql=$@"select id,empenho_id as EmpenhoId,numero,data_liquidacao as DataLiquidacao,documento_fiscal as DocumentoFiscal,historico,valor,status,motivo from sigov.liquidacao where {where} order by id desc limit @size offset @offset; select count(1) from sigov.liquidacao where {where};";
        pars.Add("size",size); pars.Add("offset",(page-1)*size);
        using var cn=_context.CreateConnection();
        using var g=await cn.QueryMultipleAsync(Command(sql,pars,ct)).ConfigureAwait(false);
        return new PagedResult<LiquidacaoResponse>((await g.ReadAsync<LiquidacaoRow>().ConfigureAwait(false)).AsList().Select(r=>new LiquidacaoResponse(r.Id,r.EmpenhoId,r.Numero,r.DataLiquidacao,r.DocumentoFiscal,r.Historico,r.Valor,r.Status)).ToList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));
    }

    public async Task<LiquidacaoResponse?> ObterAsync(long t,long e,long x,long id,CancellationToken ct)
    {
        const string sql=@"select id,empenho_id as EmpenhoId,numero,data_liquidacao as DataLiquidacao,documento_fiscal as DocumentoFiscal,historico,valor,status,motivo from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;";
        using var cn=_context.CreateConnection();
        var row=await cn.QuerySingleOrDefaultAsync<LiquidacaoRow>(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);
        return row is null ? null : new LiquidacaoResponse(row.Id,row.EmpenhoId,row.Numero,row.DataLiquidacao,row.DocumentoFiscal,row.Historico,row.Valor,row.Status);
    }

    public async Task<FinanceiroResultadoComando> CriarAsync(long t,long e,long x,long empenhoId,int ano,LiquidacaoCreateRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"liquidacao.criar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},empenhoId,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return new FinanceiroResultadoComando(docReplay,true);
            }

            // Snapshot sem lock para localizar o pai
            var snapOd=await cn.ExecuteScalarAsync<long?>(new CommandDefinition(
                @"select orcamento_despesa_id from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@empenhoId and is_deleted=false",
                new{t,e,x,empenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapOd is null) throw new InvalidOperationException("Empengo não encontrado.");

            // Lock na ordem pai -> filho: orcamento_despesa antes de empenho
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=snapOd.Value},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Lock empenho (revalidação sob lock)
            var emp=await cn.QueryFirstOrDefaultAsync<EmpenhoSaldoRow>(new CommandDefinition(
                @"select id as Id,valor_total as ValorTotal,valor_anulado as ValorAnulado,valor_liquidado as ValorLiquidado,valor_pago as ValorPago,status as Status,orcamento_despesa_id as OrcamentoDespesaId from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@empenhoId and is_deleted=false for update",
                new{t,e,x,empenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(emp is null) throw new InvalidOperationException("Empenho não encontrado.");
            if(emp.Status=="ANULADO") throw new InvalidOperationException("Não é possível liquidar empenho anulado.");

            var saldoALiq = FinanceiroInvariantes.SaldoALiquidar(emp.ValorTotal, emp.ValorAnulado, emp.ValorLiquidado);
            if(r.Valor <= 0) throw new InvalidOperationException("Valor de liquidação deve ser positivo.");
            if(r.Valor > saldoALiq) throw new InvalidOperationException($"Valor excede o saldo a liquidar. Máximo: {saldoALiq:F2}.");

            // Sequencial LAZY: gerado dentro da transação, após o check de replay
            var numero=await _seq.ProximoAsync(t,e,x,ano,"liquidacao","LIQ",ct).ConfigureAwait(false);

            var novoLiquidado = emp.ValorLiquidado + r.Valor;
            var novoStatus = FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal, emp.ValorAnulado, novoLiquidado, emp.ValorPago);

            // Update empenho
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.empenho set valor_liquidado=@novoLiquidado,status=@novoStatus,updated_at=now(),updated_by=@u where id=@empenhoId",
                new{novoLiquidado,novoStatus,empenhoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Update orcamento_despesa
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set liquidado=liquidado+@Valor,updated_at=now(),updated_by=@u where id=@orcamentoDespesaId and tenant_id=@t",
                new{t,r.Valor,orcamentoDespesaId=emp.OrcamentoDespesaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Insert liquidação
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.liquidacao (tenant_id,entidade_id,exercicio_id,empenho_id,numero,data_liquidacao,documento_fiscal,historico,valor,status,created_by) values (@t,@e,@x,@empenhoId,@numero,@DataLiquidacao,@DocumentoFiscal,@Historico,@Valor,'LIQUIDADA',@u) returning id",
                new{t,e,x,empenhoId,numero,r.DataLiquidacao,r.DocumentoFiscal,r.Historico,r.Valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento (com tenant_id!)
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="EmpenhoLiquidado",payload=JsonSerializer.Serialize(new{liquidacaoId=id,empenhoId,valor=r.Valor}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"liquidacao.criar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return new FinanceiroResultadoComando(id,false);
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task<bool> AnularAsync(long t,long e,long x,long id,AnularLiquidacaoRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.Motivo)) throw new InvalidOperationException("Motivo é obrigatório para anulação de liquidação.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"liquidacao.anular",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},id,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return true;
            }

            // Snapshot sem lock para localizar os pais
            var snapLiq=await cn.QueryFirstOrDefaultAsync<(long Id,long EmpenhoId)>(new CommandDefinition(
                @"select id,empenho_id as EmpenhoId from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapLiq.Id==0) throw new InvalidOperationException("Liquidação não encontrada.");
            var odId=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"select coalesce(orcamento_despesa_id,0) from sigov.empenho where id=@empenhoId",
                new{empenhoId=snapLiq.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Locks na ordem pai -> filho: orcamento_despesa, empenho, liquidação
            var od=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Liquidado)>(new CommandDefinition(
                @"select id,liquidado as Liquidado from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=odId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var emp=await cn.QueryFirstOrDefaultAsync<EmpenhoSaldoRow>(new CommandDefinition(
                @"select id as Id,valor_total as ValorTotal,valor_anulado as ValorAnulado,valor_liquidado as ValorLiquidado,valor_pago as ValorPago,status as Status,orcamento_despesa_id as OrcamentoDespesaId from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@empenhoId and is_deleted=false for update",
                new{t,e,x,empenhoId=snapLiq.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var liq=await cn.QueryFirstOrDefaultAsync<LiquidacaoDocRow>(new CommandDefinition(
                @"select id as Id,empenho_id as EmpenhoId,valor as Valor,status as Status from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(liq is null) throw new InvalidOperationException("Liquidação não encontrada.");
            if(liq.Status=="ANULADA") throw new InvalidOperationException("409: Liquidação já anulada.");

            // Verificar pagamentos vigentes (sob lock da liquidação)
            var pagamentosVigentes=await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                @"select count(1) from sigov.pagamento where liquidacao_id=@id and status='EFETUADO' and is_deleted=false",
                new{id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(pagamentosVigentes>0) throw new InvalidOperationException($"Não é possível anular: há {pagamentosVigentes} pagamento(s) vigente(s). Cancele os pagamentos primeiro.");

            // Decrementar (sem Math.Max: inconsistência fica explícita)
            if(emp is null) throw new InvalidOperationException("Empengo não encontrado.");
            if(emp.ValorLiquidado < liq.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            var novoLiquidado = emp.ValorLiquidado - liq.Valor;
            var novoStatus = FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal, emp.ValorAnulado, novoLiquidado, emp.ValorPago);

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.liquidacao set status='ANULADA',motivo=@Motivo,updated_at=now(),updated_by=@u where id=@id",
                new{id,r.Motivo,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.empenho set valor_liquidado=@novoLiquidado,status=@novoStatus,updated_at=now(),updated_by=@u where id=@empenhoId",
                new{novoLiquidado,novoStatus,empenhoId=liq.EmpenhoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            if(od.Liquidado < liq.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set liquidado=liquidado-@Valor,updated_at=now(),updated_by=@u where id=@orcamentoDespesaId and tenant_id=@t",
                new{t,Valor=liq.Valor,orcamentoDespesaId=emp.OrcamentoDespesaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="LiquidacaoAnulada",payload=JsonSerializer.Serialize(new{id,empenhoId=liq.EmpenhoId,valor=liq.Valor,motivo=r.Motivo}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"liquidacao.anular",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return false;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    private sealed record LiquidacaoRow(long Id,long EmpenhoId,string Numero,DateOnly DataLiquidacao,string? DocumentoFiscal,string Historico,decimal Valor,string Status,string? Motivo);
    private sealed record LiquidacaoDocRow(long Id,long EmpenhoId,decimal Valor,string Status);
    private sealed record EmpenhoSaldoRow(long Id,decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago,string Status,long OrcamentoDespesaId);
}

// ===========================================================================
// Pagamento
// ===========================================================================
public sealed class PagamentoRepository : BaseRepository, IPagamentoRepository
{
    private readonly DapperContext _context;
    private readonly IFinanceiroSequencialService _seq;
    public PagamentoRepository(DapperContext c,IFinanceiroSequencialService seq) { _context=c; _seq=seq; }

    public async Task<PagedResult<PagamentoResponse>> ListarAsync(long t,long e,long x,PagamentoFiltro f,CancellationToken ct)
    {
        var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);
        var where="tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false";
        var pars=new DynamicParameters(); pars.Add("t",t); pars.Add("e",e); pars.Add("x",x);
        if(!string.IsNullOrWhiteSpace(f.Numero)){where+=" and numero like @Numero"; pars.Add("Numero",$"%{f.Numero}%");}
        if(f.LiquidacaoId.HasValue){where+=" and liquidacao_id=@LiquidacaoId"; pars.Add("LiquidacaoId",f.LiquidacaoId.Value);}
        if(!string.IsNullOrWhiteSpace(f.Status)){where+=" and status=@Status"; pars.Add("Status",f.Status);}
        var sql=$@"select id,liquidacao_id as LiquidacaoId,numero,data_pagamento as DataPagamento,forma_pagamento as FormaPagamento,conta_bancaria as ContaBancaria,historico,valor,status,motivo from sigov.pagamento where {where} order by id desc limit @size offset @offset; select count(1) from sigov.pagamento where {where};";
        pars.Add("size",size); pars.Add("offset",(page-1)*size);
        using var cn=_context.CreateConnection();
        using var g=await cn.QueryMultipleAsync(Command(sql,pars,ct)).ConfigureAwait(false);
        return new PagedResult<PagamentoResponse>((await g.ReadAsync<PagamentoRow>().ConfigureAwait(false)).AsList().Select(r=>new PagamentoResponse(r.Id,r.LiquidacaoId,r.Numero,r.DataPagamento,r.FormaPagamento,r.ContaBancaria,r.Historico,r.Valor,r.Status)).ToList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));
    }

    public async Task<PagamentoResponse?> ObterAsync(long t,long e,long x,long id,CancellationToken ct)
    {
        const string sql=@"select id,liquidacao_id as LiquidacaoId,numero,data_pagamento as DataPagamento,forma_pagamento as FormaPagamento,conta_bancaria as ContaBancaria,historico,valor,status,motivo from sigov.pagamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false;";
        using var cn=_context.CreateConnection();
        var row=await cn.QuerySingleOrDefaultAsync<PagamentoRow>(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);
        return row is null ? null : new PagamentoResponse(row.Id,row.LiquidacaoId,row.Numero,row.DataPagamento,row.FormaPagamento,row.ContaBancaria,row.Historico,row.Valor,row.Status);
    }

    public async Task<FinanceiroResultadoComando> CriarAsync(long t,long e,long x,long liquidacaoId,int ano,PagamentoCreateRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"pagamento.criar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},liquidacaoId,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return new FinanceiroResultadoComando(docReplay,true);
            }

            // Lock liquidação
            // Snapshot sem lock para localizar os pais
            var snapLiqPag=await cn.QueryFirstOrDefaultAsync<(long Id,long EmpenhoId)>(new CommandDefinition(
                @"select id,empenho_id as EmpenhoId from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@liquidacaoId and is_deleted=false",
                new{t,e,x,liquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapLiqPag.Id==0) throw new InvalidOperationException("Liquidação não encontrada.");
            var odIdPag=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"select coalesce(orcamento_despesa_id,0) from sigov.empenho where id=@empenhoId",
                new{empenhoId=snapLiqPag.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Locks na ordem pai -> filho: orcamento_despesa, empenho, liquidação
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=odIdPag},nx,cancellationToken:ct)).ConfigureAwait(false);
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@empenhoId and is_deleted=false for update",
                new{t,e,x,empenhoId=snapLiqPag.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var liq=await cn.QueryFirstOrDefaultAsync<(long Id,long EmpenhoId,decimal Valor)>(new CommandDefinition(
                @"select id,empenho_id,valor from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@liquidacaoId and status='LIQUIDADA' and is_deleted=false for update",
                new{t,e,x,liquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(liq.Id==0) throw new InvalidOperationException("Liquidação não encontrada ou anulada.");

            // Soma pagamentos vigentes
            var pagosAnteriores=await cn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                @"select coalesce(sum(valor),0) from sigov.pagamento where liquidacao_id=@id and status='EFETUADO' and is_deleted=false",
                new{id=liquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var saldoAPagar=liq.Valor - pagosAnteriores;
            if(r.Valor <= 0) throw new InvalidOperationException("Valor de pagamento deve ser positivo.");
            if(r.Valor > saldoAPagar) throw new InvalidOperationException($"Valor excede o saldo a pagar. Máximo: {saldoAPagar:F2}.");

            // Sequencial LAZY: gerado dentro da transação, após o check de replay
            var numero=await _seq.ProximoAsync(t,e,x,ano,"pagamento","PGT",ct).ConfigureAwait(false);

            // Releitura do empenho já sob lock
            var emp=await cn.QuerySingleAsync<(decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago,long OrcamentoDespesaId)>(new CommandDefinition(
                @"select valor_total,valor_anulado,valor_liquidado,valor_pago,orcamento_despesa_id from sigov.empenho where id=@empenhoId",
                new{empenhoId=liq.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            var novoPago=emp.ValorPago + r.Valor;
            var novoStatus=FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal,emp.ValorAnulado,emp.ValorLiquidado,novoPago);

            // Insert pagamento
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.pagamento (tenant_id,entidade_id,exercicio_id,liquidacao_id,numero,data_pagamento,forma_pagamento,conta_bancaria,historico,valor,status,created_by) values (@t,@e,@x,@liquidacaoId,@numero,@DataPagamento,@FormaPagamento,@ContaBancaria,@Historico,@Valor,'EFETUADO',@u) returning id",
                new{t,e,x,liquidacaoId,numero,r.DataPagamento,r.FormaPagamento,r.ContaBancaria,r.Historico,r.Valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Update empenho
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.empenho set valor_pago=@novoPago,status=@novoStatus,updated_at=now(),updated_by=@u where id=@empenhoId",
                new{novoPago,novoStatus,empenhoId=liq.EmpenhoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Update orcamento_despesa
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set pago=pago+@Valor,updated_at=now(),updated_by=@u where id=@orcamentoDespesaId and tenant_id=@t",
                new{t,r.Valor,orcamentoDespesaId=emp.OrcamentoDespesaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="PagamentoRealizado",payload=JsonSerializer.Serialize(new{pagamentoId=id,liquidacaoId,valor=r.Valor}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"pagamento.criar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return new FinanceiroResultadoComando(id,false);
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task<bool> CancelarAsync(long t,long e,long x,long id,CancelarPagamentoRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.Motivo)) throw new InvalidOperationException("Motivo é obrigatório para cancelamento de pagamento.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"pagamento.cancelar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},id,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return true;
            }

            // Snapshot sem lock para localizar os pais
            var snapPag=await cn.QueryFirstOrDefaultAsync<(long Id,long LiquidacaoId)>(new CommandDefinition(
                @"select id,liquidacao_id as LiquidacaoId from sigov.pagamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapPag.Id==0) throw new InvalidOperationException("Pagamento não encontrado.");
            var paisPag=await cn.QueryFirstOrDefaultAsync<(long EmpenhoId,long OdId)>(new CommandDefinition(
                @"select em.id as EmpenhoId,coalesce(em.orcamento_despesa_id,0) as OdId from sigov.liquidacao l join sigov.empenho em on em.id=l.empenho_id where l.id=@lid",
                new{lid=snapPag.LiquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Locks na ordem pai -> filho: orcamento_despesa, empenho, liquidacao, pagamento
            var odPag=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Pago)>(new CommandDefinition(
                @"select id,pago as Pago from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=paisPag.OdId},nx,cancellationToken:ct)).ConfigureAwait(false);
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@eid and is_deleted=false for update",
                new{t,e,x,eid=paisPag.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@lid and is_deleted=false for update",
                new{t,e,x,lid=snapPag.LiquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Lock pagamento (revalidacao sob lock)
            var pag=await cn.QueryFirstOrDefaultAsync<(long Id,long LiquidacaoId,decimal Valor,string Status)>(new CommandDefinition(
                @"select id,liquidacao_id,valor,status from sigov.pagamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(pag.Id==0) throw new InvalidOperationException("Pagamento não encontrado.");
            if(pag.Status=="CANCELADO") throw new InvalidOperationException("409: Pagamento já cancelado.");

            // Lock liquidação→empenho
            var liq=await cn.QueryFirstOrDefaultAsync<(long Id,long EmpenhoId)>(new CommandDefinition(
                @"select id,empenho_id from sigov.liquidacao where id=@lid",
                new{lid=pag.LiquidacaoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            var emp=await cn.QueryFirstOrDefaultAsync<(decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago,long OrcamentoDespesaId)>(new CommandDefinition(
                @"select valor_total,valor_anulado,valor_liquidado,valor_pago,orcamento_despesa_id from sigov.empenho where id=@eid",
                new{eid=liq.EmpenhoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            if(emp.ValorPago < pag.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            var novoPago=emp.ValorPago - pag.Valor;
            var novoStatus=FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal,emp.ValorAnulado,emp.ValorLiquidado,novoPago);

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.pagamento set status='CANCELADO',motivo=@Motivo,updated_at=now(),updated_by=@u where id=@id",
                new{id,r.Motivo,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.empenho set valor_pago=@novoPago,status=@novoStatus,updated_at=now(),updated_by=@u where id=@eid",
                new{novoPago,novoStatus,eid=liq.EmpenhoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            if(odPag.Pago < pag.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_despesa set pago=pago-@Valor,updated_at=now(),updated_by=@u where id=@oid and tenant_id=@t",
                new{t,Valor=pag.Valor,oid=emp.OrcamentoDespesaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="PagamentoCancelado",payload=JsonSerializer.Serialize(new{id,liquidacaoId=pag.LiquidacaoId,valor=pag.Valor,motivo=r.Motivo}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"pagamento.cancelar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return false;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    private sealed record PagamentoRow(long Id,long LiquidacaoId,string Numero,DateOnly DataPagamento,string FormaPagamento,string? ContaBancaria,string Historico,decimal Valor,string Status,string? Motivo);
}

// ===========================================================================
// Receita (Lançamentos e Arrecadações)
// ===========================================================================
public sealed class ReceitaRepository : BaseRepository, IReceitaRepository
{
    private readonly DapperContext _context;
    private readonly IFinanceiroSequencialService _seq;
    public ReceitaRepository(DapperContext c,IFinanceiroSequencialService seq) { _context=c; _seq=seq; }

    public async Task<PagedResult<ReceitaLancamentoResponse>> ListarLancamentosAsync(long t,long e,long x,ReceitaLancamentoFiltro f,CancellationToken ct)
    {
        var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);
        var where="rl.tenant_id=@t and rl.entidade_id=@e and rl.exercicio_id=@x and rl.is_deleted=false";
        var pars=new DynamicParameters(); pars.Add("t",t); pars.Add("e",e); pars.Add("x",x);
        if(!string.IsNullOrWhiteSpace(f.Numero)){where+=" and rl.numero like @Numero"; pars.Add("Numero",$"%{f.Numero}%");}
        if(!string.IsNullOrWhiteSpace(f.Status)){where+=" and rl.status=@Status"; pars.Add("Status",f.Status);}
        if(f.ContribuintePessoaId.HasValue){where+=" and rl.contribuinte_pessoa_id=@ContribuintePessoaId"; pars.Add("ContribuintePessoaId",f.ContribuintePessoaId.Value);}
        const string select="rl.id,rl.orcamento_receita_id as OrcamentoReceitaId,rl.numero,rl.data_lancamento as DataLancamento,rl.contribuinte_pessoa_id as ContribuintePessoaId,p.nome as Contribuinte,rl.historico,rl.valor,coalesce((select sum(ra.valor) from sigov.receita_arrecadacao ra where ra.receita_lancamento_id=rl.id and ra.status='ARRECADADA'),0) as Arrecadado,rl.status";
        var sql=$"select {select} from sigov.receita_lancamento rl left join sigov.pessoa p on p.id=rl.contribuinte_pessoa_id where {where} order by rl.id desc limit @size offset @offset; select count(1) from sigov.receita_lancamento rl where {where};";
        pars.Add("size",size); pars.Add("offset",(page-1)*size);
        using var cn=_context.CreateConnection();
        using var g=await cn.QueryMultipleAsync(Command(sql,pars,ct)).ConfigureAwait(false);
        return new PagedResult<ReceitaLancamentoResponse>((await g.ReadAsync<ReceitaLancamentoResponse>().ConfigureAwait(false)).AsList(),page,size,await g.ReadSingleAsync<long>().ConfigureAwait(false));
    }

    public async Task<ReceitaLancamentoResponse?> ObterLancamentoAsync(long t,long e,long x,long id,CancellationToken ct)
    {
        const string sql=@"select rl.id,rl.orcamento_receita_id as OrcamentoReceitaId,rl.numero,rl.data_lancamento as DataLancamento,rl.contribuinte_pessoa_id as ContribuintePessoaId,p.nome as Contribuinte,rl.historico,rl.valor,coalesce((select sum(ra.valor) from sigov.receita_arrecadacao ra where ra.receita_lancamento_id=rl.id and ra.status='ARRECADADA'),0) as Arrecadado,rl.status from sigov.receita_lancamento rl left join sigov.pessoa p on p.id=rl.contribuinte_pessoa_id where rl.tenant_id=@t and rl.entidade_id=@e and rl.exercicio_id=@x and rl.id=@id and rl.is_deleted=false;";
        using var cn=_context.CreateConnection();
        return await cn.QuerySingleOrDefaultAsync<ReceitaLancamentoResponse>(Command(sql,new{t,e,x,id},ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReceitaArrecadacaoResponse>> ListarArrecadacoesAsync(long t,long e,long x,long lancamentoId,CancellationToken ct)
    {
        const string sql=@"select id,receita_lancamento_id as ReceitaLancamentoId,numero,data_arrecadacao as DataArrecadacao,forma_arrecadacao as FormaArrecadacao,valor,historico,status from sigov.receita_arrecadacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and receita_lancamento_id=@lid and is_deleted=false order by id desc limit 50;";
        using var cn=_context.CreateConnection();
        return (await cn.QueryAsync<ReceitaArrecadacaoResponse>(Command(sql,new{t,e,x,lid=lancamentoId},ct)).ConfigureAwait(false)).AsList();
    }

    public async Task<FinanceiroResultadoComando> CriarLancamentoAsync(long t,long e,long x,int ano,ReceitaLancamentoCreateRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"receita.lancamento.criar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return new FinanceiroResultadoComando(docReplay,true);
            }

            if(r.Valor<=0m) throw new InvalidOperationException("Valor do lançamento deve ser positivo.");

            if(r.ContribuintePessoaId is >0)
            {
                var contribuinteOk=await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                    @"select count(1) from sigov.pessoa where id=@pid and tenant_id=@t and ativo=true and is_deleted=false and (entidade_id is null or entidade_id=@e) and (exercicio_id is null or exercicio_id=@x)",
                    new{t,e,x,pid=r.ContribuintePessoaId.Value},nx,cancellationToken:ct)).ConfigureAwait(false);
                if(contribuinteOk==0) throw new InvalidOperationException("Contribuinte não localizado ou inativo no contexto selecionado.");
            }

            // Lock da previsao (pai) com validacao de contexto
            var oreRow=await cn.QueryFirstOrDefaultAsync<long>(new CommandDefinition(
                @"select id from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@OrcamentoReceitaId and ativo=true and is_deleted=false for update",
                new{t,e,x,r.OrcamentoReceitaId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(oreRow==0) throw new InvalidOperationException("Previsão de receita não localizada ou inativa no contexto selecionado.");

            // Sequencial LAZY: gerado dentro da transacao, apos o check de replay
            var numero=await _seq.ProximoAsync(t,e,x,ano,"receita_lancamento","REC",ct).ConfigureAwait(false);

            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.receita_lancamento (tenant_id,entidade_id,exercicio_id,orcamento_receita_id,numero,data_lancamento,contribuinte_pessoa_id,historico,valor,status,created_by) values (@t,@e,@x,@OrcamentoReceitaId,@numero,@DataLancamento,@ContribuintePessoaId,@Historico,@Valor,'LANCADA',@u) returning id",
                new{t,e,x,r.OrcamentoReceitaId,numero,r.DataLancamento,r.ContribuintePessoaId,r.Historico,r.Valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_receita set lancado=lancado+@Valor,updated_at=now(),updated_by=@u where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@OrcamentoReceitaId",
                new{t,e,x,r.OrcamentoReceitaId,r.Valor,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"receita.lancamento.criar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return new FinanceiroResultadoComando(id,false);
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task<FinanceiroResultadoComando> ArrecadarAsync(long t,long e,long x,long lancamentoId,int ano,ReceitaArrecadacaoCreateRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"receita.arrecadacao.criar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},lancamentoId,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return new FinanceiroResultadoComando(docReplay,true);
            }

            // Snapshot sem lock para localizar os pais
            var snapLanc=await cn.QueryFirstOrDefaultAsync<(long Id,long OrcamentoReceitaId)>(new CommandDefinition(
                @"select id,orcamento_receita_id as OrcamentoReceitaId from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@lancamentoId and is_deleted=false",
                new{t,e,x,lancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapLanc.Id==0) throw new InvalidOperationException("Lançamento não encontrado.");

            // Locks na ordem pai -> filho: orcamento_receita, lancamento
            await cn.QueryFirstOrDefaultAsync(new CommandDefinition(
                @"select id from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=snapLanc.OrcamentoReceitaId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Lock lançamento
            var lanc=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Valor,string Status,long OrcamentoReceitaId)>(new CommandDefinition(
                @"select id,valor,status,orcamento_receita_id from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@lancamentoId and is_deleted=false for update",
                new{t,e,x,lancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(lanc.Id==0) throw new InvalidOperationException("Lançamento não encontrado.");
            if(lanc.Status=="CANCELADA") throw new InvalidOperationException("Lançamento já cancelado.");

            // Soma arrecadações vigentes
            var arrecadadoAnterior=await cn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                @"select coalesce(sum(valor),0) from sigov.receita_arrecadacao where receita_lancamento_id=@lid and status='ARRECADADA' and is_deleted=false",
                new{lid=lancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var saldoRemanescente=lanc.Valor - arrecadadoAnterior;
            if(r.Valor <= 0) throw new InvalidOperationException("Valor de arrecadação deve ser positivo.");
            if(r.Valor > saldoRemanescente) throw new InvalidOperationException($"Valor excede o saldo a arrecadar. Máximo: {saldoRemanescente:F2}.");

            var novoArrecadado=arrecadadoAnterior + r.Valor;
            var novoStatus=FinanceiroInvariantes.DerivarStatusLancamento(lanc.Valor, novoArrecadado);

            // Sequencial LAZY: gerado dentro da transacao, apos o check de replay
            var numero=await _seq.ProximoAsync(t,e,x,ano,"receita_arrecadacao","ARQ",ct).ConfigureAwait(false);

            // Insert arrecadação
            var id=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"insert into sigov.receita_arrecadacao (tenant_id,entidade_id,exercicio_id,receita_lancamento_id,numero,data_arrecadacao,forma_arrecadacao,valor,historico,status,created_by) values (@t,@e,@x,@lancamentoId,@numero,@DataArrecadacao,@FormaArrecadacao,@Valor,@Historico,'ARRECADADA',@u) returning id",
                new{t,e,x,lancamentoId,numero,r.DataArrecadacao,r.FormaArrecadacao,r.Valor,r.Historico,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Update lançamento status
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.receita_lancamento set status=@novoStatus,updated_at=now(),updated_by=@u where id=@lancamentoId",
                new{novoStatus,lancamentoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Update orcamento_receita
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_receita set arrecadado=arrecadado+@Valor,updated_at=now(),updated_by=@u where id=@oid and tenant_id=@t",
                new{t,r.Valor,oid=lanc.OrcamentoReceitaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="ArrecadacaoRealizada",payload=JsonSerializer.Serialize(new{arrecadacaoId=id,lancamentoId,valor=r.Valor}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"receita.arrecadacao.criar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return new FinanceiroResultadoComando(id,false);
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task<bool> CancelarLancamentoAsync(long t,long e,long x,long id,CancelarLancamentoRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.Motivo)) throw new InvalidOperationException("Motivo é obrigatório para cancelamento de lançamento.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"receita.lancamento.cancelar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},id,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return true;
            }

            // Snapshot sem lock para localizar os pais
            var snapLancC=await cn.QueryFirstOrDefaultAsync<(long Id,long OrcamentoReceitaId)>(new CommandDefinition(
                @"select id,orcamento_receita_id as OrcamentoReceitaId from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapLancC.Id==0) throw new InvalidOperationException("Lançamento não encontrado.");

            // Locks na ordem pai -> filho: orcamento_receita, lancamento
            var oreLancC=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Lancado)>(new CommandDefinition(
                @"select id,lancado as Lancado from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=snapLancC.OrcamentoReceitaId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var lanc=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Valor,string Status,long OrcamentoReceitaId)>(new CommandDefinition(
                @"select id,valor,status,orcamento_receita_id from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(lanc.Id==0) throw new InvalidOperationException("Lançamento não encontrado.");
            if(lanc.Status=="CANCELADA") throw new InvalidOperationException("409: Lançamento já cancelado.");

            // Nao cancela arrecadacoes vigentes silenciosamente
            var abVigentes=await cn.ExecuteScalarAsync<int>(new CommandDefinition(
                @"select count(1) from sigov.receita_arrecadacao where receita_lancamento_id=@id and status='ARRECADADA' and is_deleted=false",
                new{id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(abVigentes>0) throw new InvalidOperationException($"Não é possível cancelar o lançamento: há {abVigentes} arrecadação(ões) vigente(s). Cancele as arrecadações primeiro.");

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.receita_lancamento set status='CANCELADA',motivo=@Motivo,updated_at=now(),updated_by=@u where id=@id",
                new{id,r.Motivo,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Decompo somente o lancado: arrecadado ja foi decomposto por cada cancelamento de arrecadacao
            if(oreLancC.Lancado < lanc.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_receita set lancado=lancado-@Valor,updated_at=now(),updated_by=@u where id=@oid and tenant_id=@t",
                new{t,Valor=lanc.Valor,oid=lanc.OrcamentoReceitaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="LancamentoCancelado",payload=JsonSerializer.Serialize(new{id,valor=lanc.Valor,motivo=r.Motivo}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"receita.lancamento.cancelar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return false;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

    public async Task<bool> CancelarArrecadacaoAsync(long t,long e,long x,long id,CancelarArrecadacaoRequest r,long? u,string? idempotencyKey,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(r.Motivo)) throw new InvalidOperationException("Motivo é obrigatório para cancelamento de arrecadação.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"receita.arrecadacao.cancelar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},id,r}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return true;
            }

            // Snapshot sem lock para localizar os pais
            var snapArq=await cn.QueryFirstOrDefaultAsync<(long Id,long LancamentoId)>(new CommandDefinition(
                @"select id,receita_lancamento_id as LancamentoId from sigov.receita_arrecadacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(snapArq.Id==0) throw new InvalidOperationException("Arrecadação não encontrada.");
            var oidArq=await cn.ExecuteScalarAsync<long>(new CommandDefinition(
                @"select coalesce(orcamento_receita_id,0) from sigov.receita_lancamento where id=@lid",
                new{lid=snapArq.LancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Locks na ordem pai -> filho: orcamento_receita, lancamento, arrecadacao
            var oreArq=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Arrecadado)>(new CommandDefinition(
                @"select id,arrecadado as Arrecadado from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@oid and is_deleted=false for update",
                new{t,e,x,oid=oidArq},nx,cancellationToken:ct)).ConfigureAwait(false);
            var lanc=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Valor,long OrcamentoReceitaId)>(new CommandDefinition(
                @"select id,valor,orcamento_receita_id from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@lid and is_deleted=false for update",
                new{t,e,x,lid=snapArq.LancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var arq=await cn.QueryFirstOrDefaultAsync<(long Id,long LancamentoId,decimal Valor,string Status)>(new CommandDefinition(
                @"select id,receita_lancamento_id,valor,status from sigov.receita_arrecadacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(arq.Id==0) throw new InvalidOperationException("Arrecadação não encontrada.");
            if(arq.Status=="CANCELADA") throw new InvalidOperationException("409: Arrecadação já cancelada.");

            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.receita_arrecadacao set status='CANCELADA',motivo=@Motivo,updated_at=now(),updated_by=@u where id=@id",
                new{id,r.Motivo,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Recalcular status do lançamento
            var arrecadadoRestante=await cn.ExecuteScalarAsync<decimal>(new CommandDefinition(
                @"select coalesce(sum(valor),0) from sigov.receita_arrecadacao where receita_lancamento_id=@lid and status='ARRECADADA' and is_deleted=false",
                new{lid=arq.LancamentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var novoStatus=FinanceiroInvariantes.DerivarStatusLancamento(lanc.Valor,arrecadadoRestante);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.receita_lancamento set status=@novoStatus,updated_at=now(),updated_by=@u where id=@lid",
                new{novoStatus,lid=arq.LancamentoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Decompor orcamento_receita (sem greatest: inconsistencia fica explicita)
            if(oreArq.Arrecadado < arq.Valor) throw new InvalidOperationException(FinanceiroInvariantes.Inconsistencia);
            await cn.ExecuteAsync(new CommandDefinition(
                @"update sigov.orcamento_receita set arrecadado=arrecadado-@Valor,updated_at=now(),updated_by=@u where id=@oid and tenant_id=@t",
                new{t,Valor=arq.Valor,oid=lanc.OrcamentoReceitaId,u},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Evento
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@u)",
                new{t,e,x,tipo="ArrecadacaoCancelada",payload=JsonSerializer.Serialize(new{id,lancamentoId=arq.LancamentoId,valor=arq.Valor,motivo=r.Motivo}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"receita.arrecadacao.cancelar",chave,id,ct).ConfigureAwait(false);
            nx.Commit();
            return false;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }
}

// ===========================================================================
// Dashboard
// ===========================================================================
public sealed class FinanceiroDashboardRepository : BaseRepository, IFinanceiroDashboardRepository
{
    private readonly DapperContext _context; public FinanceiroDashboardRepository(DapperContext c)=>_context=c;
    public async Task<FinanceiroDashboardResponse> ObterAsync(long t,long e,long x,CancellationToken ct){const string sql="select coalesce(sum(dotacao_inicial+suplementacoes-reducoes),0) as OrcamentoAutorizado,coalesce(sum(empenhado),0) as Empenhado,coalesce(sum(liquidado),0) as Liquidado,coalesce(sum(pago),0) as Pago,coalesce(sum(dotacao_inicial+suplementacoes-reducoes-reservado-empenhado),0) as SaldoDisponivel from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false; select coalesce(sum(previsao_atualizada),0) as ReceitaPrevista,coalesce(sum(lancado),0) as ReceitaLancada,coalesce(sum(arrecadado),0) as ReceitaArrecadada from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false;"; using var cn=_context.CreateConnection(); using var g=await cn.QueryMultipleAsync(Command(sql,new{t,e,x},ct)).ConfigureAwait(false); var d=await g.ReadSingleAsync<FinanceiroResumoDespesaResponse>().ConfigureAwait(false); var r=await g.ReadSingleAsync<FinanceiroResumoReceitaResponse>().ConfigureAwait(false); return new FinanceiroDashboardResponse(d,r,DateTimeOffset.UtcNow);}
}

// ===========================================================================
// Exportação real (CSV/JSON)
// ===========================================================================
public sealed class FinanceiroExportacaoRepository : BaseRepository, IFinanceiroExportacaoRepository
{
    private readonly DapperContext _context; public FinanceiroExportacaoRepository(DapperContext c)=>_context=c;

    public async Task<byte[]> ExportarAsync(long t,long e,long x,string recurso,string formato,CancellationToken ct)
    {
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        object? data = recurso.ToLowerInvariant() switch
        {
            "empenhos" => await cn.QueryAsync(new CommandDefinition(@"select id,numero,data_empenho,fornecedor_pessoa_id,valor_total,valor_anulado,valor_liquidado,valor_pago,status from sigov.empenho where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            "liquidacoes" => await cn.QueryAsync(new CommandDefinition(@"select id,empenho_id,numero,data_liquidacao,valor,status from sigov.liquidacao where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            "pagamentos" => await cn.QueryAsync(new CommandDefinition(@"select id,liquidacao_id,numero,data_pagamento,valor,status from sigov.pagamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            "receitas" => await cn.QueryAsync(new CommandDefinition(@"select id,orcamento_receita_id,numero,data_lancamento,valor,status from sigov.receita_lancamento where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            "orcamento-despesas" => await cn.QueryAsync(new CommandDefinition(@"select id,programa_id,acao_id,natureza_despesa_id,fonte_recurso_id,dotacao_inicial,suplementacoes,reducoes,reservado,empenhado,liquidado,pago from sigov.orcamento_despesa where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            "orcamento-receitas" => await cn.QueryAsync(new CommandDefinition(@"select id,natureza_receita_id,fonte_recurso_id,previsao_inicial,previsao_atualizada,lancado,arrecadado from sigov.orcamento_receita where tenant_id=@t and entidade_id=@e and exercicio_id=@x and is_deleted=false order by id",new{t,e,x},cancellationToken:ct)).ConfigureAwait(false),
            _ => Array.Empty<object>()
        };

        if(formato.Equals("json",StringComparison.OrdinalIgnoreCase))
            return JsonSerializer.SerializeToUtf8Bytes(data, new JsonSerializerOptions{WriteIndented=true});

        // CSV: usa reflection sem dynamic (Dapper row)
        var list=data as IEnumerable<object>;
        if(list==null||!list.Any()) return System.Text.Encoding.UTF8.GetBytes("[]\r\n");
        var rows=list.ToList();
        var props=rows[0].GetType().GetProperties();
        Func<object,System.Reflection.PropertyInfo,string> valor=(row,p)=>p.GetValue(row)?.ToString()??"";
        var headers=string.Join(";",props.Select(p=>p.Name));
        var lines=props.Length>0 ? rows.Select(row=>string.Join(";",props.Select(p=>valor(row,p)))).ToList() : new List<string>();
        var csv=headers+"\r\n"+string.Join("\r\n",lines)+"\r\n";
        return System.Text.Encoding.UTF8.GetBytes(csv);
    }
}

// ===========================================================================
// Conferência financeira
// ===========================================================================
public sealed class FinanceiroConferenciaRepository : BaseRepository, IFinanceiroConferenciaRepository
{
    private readonly DapperContext _context; public FinanceiroConferenciaRepository(DapperContext c)=>_context=c;

    // Conjunto fechado de tipos de documento da conferência (ordem canônica)
    private static readonly string[] TiposConferencia={"EMPENHO","LIQUIDACAO","PAGAMENTO","LANCAMENTO","ARRECADACAO","LIQUIDACAO_EMPENHO","PAGAMENTO_EMPENHO"};

    private static string? OrigemDivergencia(decimal diferenca)=>diferenca==0m?null:(diferenca>0m?"Acumulador acima da soma dos documentos.":"Acumulador abaixo da soma dos documentos.");

    private sealed record AlvoConferencia(string Tabela,string Coluna,string SqlCalculado);

    // Dicionário fechado tipo -> (tabela,coluna,SQL do recálculo dentro da tx)
    private static readonly Dictionary<string,AlvoConferencia> AlvosConferencia=new(StringComparer.Ordinal)
    {
        ["EMPENHO"]=new("orcamento_despesa","empenhado","select coalesce(sum(valor_total-valor_anulado),0) from sigov.empenho where orcamento_despesa_id=@id and is_deleted=false"),
        ["LIQUIDACAO"]=new("orcamento_despesa","liquidado","select coalesce(sum(lq.valor),0) from sigov.liquidacao lq join sigov.empenho e on e.id=lq.empenho_id where e.orcamento_despesa_id=@id and lq.status='LIQUIDADA' and lq.is_deleted=false"),
        ["PAGAMENTO"]=new("orcamento_despesa","pago","select coalesce(sum(pg.valor),0) from sigov.pagamento pg join sigov.liquidacao lq on lq.id=pg.liquidacao_id join sigov.empenho e on e.id=lq.empenho_id where e.orcamento_despesa_id=@id and pg.status='EFETUADO' and pg.is_deleted=false"),
        ["LANCAMENTO"]=new("orcamento_receita","lancado","select coalesce(sum(valor),0) from sigov.receita_lancamento where orcamento_receita_id=@id and status<>'CANCELADA' and is_deleted=false"),
        ["ARRECADACAO"]=new("orcamento_receita","arrecadado","select coalesce(sum(ra.valor),0) from sigov.receita_arrecadacao ra join sigov.receita_lancamento rl on rl.id=ra.receita_lancamento_id where rl.orcamento_receita_id=@id and ra.status='ARRECADADA' and ra.is_deleted=false"),
        ["LIQUIDACAO_EMPENHO"]=new("empenho","valor_liquidado","select coalesce(sum(valor),0) from sigov.liquidacao where empenho_id=@id and status='LIQUIDADA' and is_deleted=false"),
        ["PAGAMENTO_EMPENHO"]=new("empenho","valor_pago","select coalesce(sum(pg.valor),0) from sigov.pagamento pg join sigov.liquidacao lq on lq.id=pg.liquidacao_id where lq.empenho_id=@id and pg.status='EFETUADO' and pg.is_deleted=false")
    };

    // CTE global com 7 branches: acumulador registrado vs soma dos documentos, sempre no contexto t/e/x
    private const string SqlCteConferencia=@"with base as (
    select 'EMPENHO' as tipo_documento,od.id as documento_id,
           case when a.id is null then '(dotação sem ação)' else 'Ação '||a.codigo||' '||a.nome||' | Natureza '||nd.codigo||' '||nd.nome||' | Fonte '||f.codigo end as documento_numero,
           od.empenhado as valor_registrado,
           coalesce((select sum(e.valor_total-e.valor_anulado) from sigov.empenho e where e.orcamento_despesa_id=od.id and e.is_deleted=false),0) as valor_calculado
    from sigov.orcamento_despesa od
    left join sigov.acao a on a.id=od.acao_id and a.tenant_id=@t and not a.is_deleted
    left join sigov.natureza_despesa nd on nd.id=od.natureza_despesa_id and nd.tenant_id=@t and not nd.is_deleted
    left join sigov.fonte_recurso f on f.id=od.fonte_recurso_id and f.tenant_id=@t and not f.is_deleted
    where od.tenant_id=@t and od.entidade_id=@e and od.exercicio_id=@x and od.is_deleted=false
    union all
    select 'LIQUIDACAO',od.id,
           case when a.id is null then '(dotação sem ação)' else 'Ação '||a.codigo||' '||a.nome||' | Natureza '||nd.codigo||' '||nd.nome||' | Fonte '||f.codigo end,
           od.liquidado,
           coalesce((select sum(lq.valor) from sigov.liquidacao lq join sigov.empenho e on e.id=lq.empenho_id where e.orcamento_despesa_id=od.id and lq.status='LIQUIDADA' and lq.is_deleted=false),0)
    from sigov.orcamento_despesa od
    left join sigov.acao a on a.id=od.acao_id and a.tenant_id=@t and not a.is_deleted
    left join sigov.natureza_despesa nd on nd.id=od.natureza_despesa_id and nd.tenant_id=@t and not nd.is_deleted
    left join sigov.fonte_recurso f on f.id=od.fonte_recurso_id and f.tenant_id=@t and not f.is_deleted
    where od.tenant_id=@t and od.entidade_id=@e and od.exercicio_id=@x and od.is_deleted=false
    union all
    select 'PAGAMENTO',od.id,
           case when a.id is null then '(dotação sem ação)' else 'Ação '||a.codigo||' '||a.nome||' | Natureza '||nd.codigo||' '||nd.nome||' | Fonte '||f.codigo end,
           od.pago,
           coalesce((select sum(pg.valor) from sigov.pagamento pg join sigov.liquidacao lq on lq.id=pg.liquidacao_id join sigov.empenho e on e.id=lq.empenho_id where e.orcamento_despesa_id=od.id and pg.status='EFETUADO' and pg.is_deleted=false),0)
    from sigov.orcamento_despesa od
    left join sigov.acao a on a.id=od.acao_id and a.tenant_id=@t and not a.is_deleted
    left join sigov.natureza_despesa nd on nd.id=od.natureza_despesa_id and nd.tenant_id=@t and not nd.is_deleted
    left join sigov.fonte_recurso f on f.id=od.fonte_recurso_id and f.tenant_id=@t and not f.is_deleted
    where od.tenant_id=@t and od.entidade_id=@e and od.exercicio_id=@x and od.is_deleted=false
    union all
    select 'LANCAMENTO',ore.id,'Previsão '||nr.codigo||' '||nr.nome||' | Fonte '||fr.codigo,
           ore.lancado,
           coalesce((select sum(rl.valor) from sigov.receita_lancamento rl where rl.orcamento_receita_id=ore.id and rl.status<>'CANCELADA' and rl.is_deleted=false),0)
    from sigov.orcamento_receita ore
    left join sigov.natureza_receita nr on nr.id=ore.natureza_receita_id and nr.tenant_id=@t and not nr.is_deleted
    left join sigov.fonte_recurso fr on fr.id=ore.fonte_recurso_id and fr.tenant_id=@t and not fr.is_deleted
    where ore.tenant_id=@t and ore.entidade_id=@e and ore.exercicio_id=@x and ore.is_deleted=false
    union all
    select 'ARRECADACAO',ore.id,'Previsão '||nr.codigo||' '||nr.nome||' | Fonte '||fr.codigo,
           ore.arrecadado,
           coalesce((select sum(ra.valor) from sigov.receita_arrecadacao ra join sigov.receita_lancamento rl on rl.id=ra.receita_lancamento_id where rl.orcamento_receita_id=ore.id and ra.status='ARRECADADA' and ra.is_deleted=false),0)
    from sigov.orcamento_receita ore
    left join sigov.natureza_receita nr on nr.id=ore.natureza_receita_id and nr.tenant_id=@t and not nr.is_deleted
    left join sigov.fonte_recurso fr on fr.id=ore.fonte_recurso_id and fr.tenant_id=@t and not fr.is_deleted
    where ore.tenant_id=@t and ore.entidade_id=@e and ore.exercicio_id=@x and ore.is_deleted=false
    union all
    select 'LIQUIDACAO_EMPENHO',emp.id,emp.numero,
           emp.valor_liquidado,
           coalesce((select sum(lq.valor) from sigov.liquidacao lq where lq.empenho_id=emp.id and lq.status='LIQUIDADA' and lq.is_deleted=false),0)
    from sigov.empenho emp
    where emp.tenant_id=@t and emp.entidade_id=@e and emp.exercicio_id=@x and emp.is_deleted=false
    union all
    select 'PAGAMENTO_EMPENHO',emp.id,emp.numero,
           emp.valor_pago,
           coalesce((select sum(pg.valor) from sigov.pagamento pg join sigov.liquidacao lq on lq.id=pg.liquidacao_id where lq.empenho_id=emp.id and pg.status='EFETUADO' and pg.is_deleted=false),0)
    from sigov.empenho emp
    where emp.tenant_id=@t and emp.entidade_id=@e and emp.exercicio_id=@x and emp.is_deleted=false
)
,base_calc as (select b.*,b.valor_registrado-b.valor_calculado as diferenca from base b)
,filtrado as (select * from base_calc where (@tipo is null or tipo_documento=@tipo) and (@situacao is null or (@situacao='OK' and diferenca=0) or (@situacao='DIVERGENTE' and diferenca<>0)))";

    public async Task<ConferenciaResponse> ConferirAsync(long t,long e,long x,ConferenciaFiltro f,CancellationToken ct)
    {
        var page=Math.Max(1,f.Page);var size=Math.Clamp(f.PageSize,1,100);
        string? tipo=null;string? situacao=null;
        if(!string.IsNullOrWhiteSpace(f.TipoDocumento))
        {
            tipo=f.TipoDocumento.Trim().ToUpperInvariant();
            if(Array.IndexOf(TiposConferencia,tipo)<0) throw new ArgumentException($"Tipo de documento inválido para conferência. Tipos válidos: {string.Join(", ",TiposConferencia)}.");
        }
        if(!string.IsNullOrWhiteSpace(f.Situacao))
        {
            situacao=f.Situacao.Trim().ToUpperInvariant();
            if(situacao!="OK"&&situacao!="DIVERGENTE") throw new ArgumentException("Situação inválida para conferência. Situações válidas: OK, DIVERGENTE.");
        }
        // Totais gerais sobre TODO o escopo antes da paginação; itens+paginação na segunda statement
        var sql=$@"{SqlCteConferencia}
select count(1) as TotalRegistros,coalesce(sum(case when abs(diferenca)>0 then 1 end),0) as TotalDivergencias,coalesce(sum(abs(diferenca)),0) as SomaAbsDiferencas from filtrado;
{SqlCteConferencia}
select tipo_documento as TipoDocumento,documento_id as DocumentoId,coalesce(documento_numero,'') as DocumentoNumero,valor_registrado as ValorRegistrado,valor_calculado as ValorCalculado,diferenca as Diferenca from filtrado order by abs(diferenca) desc,tipo_documento,documento_id limit @size offset @offset;";
        using var cn=_context.CreateConnection();
        using var g=await cn.QueryMultipleAsync(new CommandDefinition(sql,new{t,e,x,tipo,situacao,size,offset=(page-1)*size},cancellationToken:ct)).ConfigureAwait(false);
        var totais=await g.ReadSingleAsync<(long TotalRegistros,long TotalDivergencias,decimal SomaAbsDiferencas)>().ConfigureAwait(false);
        var rows=await g.ReadAsync<(string TipoDocumento,long DocumentoId,string DocumentoNumero,decimal ValorRegistrado,decimal ValorCalculado,decimal Diferenca)>().ConfigureAwait(false);
        var itens=rows.Select(r=>new ConferenciaItemResponse(r.TipoDocumento,r.DocumentoId,r.DocumentoNumero,r.ValorRegistrado,r.ValorCalculado,r.Diferenca,OrigemDivergencia(r.Diferenca))).ToList();
        return new ConferenciaResponse(itens,totais.TotalRegistros,totais.TotalDivergencias,totais.SomaAbsDiferencas,page,size,DateTimeOffset.UtcNow);
    }

    public async Task<ConferenciaItemResponse?> ConsultarItemAsync(long t,long e,long x,long documentoId,string tipoDocumento,CancellationToken ct)
    {
        var tipo=tipoDocumento.Trim().ToUpperInvariant();
        var sql=$@"{SqlCteConferencia}
select tipo_documento as TipoDocumento,documento_id as DocumentoId,coalesce(documento_numero,'') as DocumentoNumero,valor_registrado as ValorRegistrado,valor_calculado as ValorCalculado,diferenca as Diferenca from filtrado where tipo_documento=@tipo and documento_id=@id;";
        using var cn=_context.CreateConnection();
        var r=await cn.QueryFirstOrDefaultAsync<(string TipoDocumento,long DocumentoId,string DocumentoNumero,decimal ValorRegistrado,decimal ValorCalculado,decimal Diferenca)?>(new CommandDefinition(sql,new{t,e,x,tipo,situacao=(string?)null,id=documentoId},cancellationToken:ct)).ConfigureAwait(false);
        if(r is null) return null;
        return new ConferenciaItemResponse(r.Value.TipoDocumento,r.Value.DocumentoId,r.Value.DocumentoNumero,r.Value.ValorRegistrado,r.Value.ValorCalculado,r.Value.Diferenca,OrigemDivergencia(r.Value.Diferenca));
    }

    public async Task<bool> EhReplayAsync(long t,long e,long x,long documentoId,string tipoDocumento,string justificativa,string chave,CancellationToken ct)
    {
        var tipo=tipoDocumento.Trim().ToUpperInvariant();
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        var row=await cn.QueryFirstOrDefaultAsync<(long? DocumentoId,string PayloadHash)>(new CommandDefinition(
            @"select documento_id,payload_hash from sigov.financeiro_idempotencia where tenant_id=@t and escopo=@escopo and chave=@chave",
            new{t,escopo="conferencia.ajustar",chave},cancellationToken:ct)).ConfigureAwait(false);
        if(row.DocumentoId is null || row.DocumentoId.Value<=0) return false;
        return string.Equals(row.PayloadHash,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},documentoId,tipoDocumento=tipo,justificativa}),StringComparison.Ordinal);
    }

    public async Task<ConferenciaAjusteResultado> AjustarAsync(long t,long e,long x,long documentoId,string tipoDocumento,decimal diferencaPrevista,string justificativa,long? u,string? idempotencyKey,CancellationToken ct)
    {
        var tipo=tipoDocumento.Trim().ToUpperInvariant();
        if(!AlvosConferencia.TryGetValue(tipo,out var alvo)) throw new ArgumentException($"Tipo de documento inválido para ajuste de conferência. Tipos válidos: {string.Join(", ",TiposConferencia)}.");
        using var cn=(NpgsqlConnection)_context.CreateConnection();
        await cn.OpenAsync(ct).ConfigureAwait(false);
        await using var nx=await cn.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await FinanceiroContextoEscrever.GuardarAsync(cn,nx,t,e,x,ct).ConfigureAwait(false);

            var chave=FinanceiroIdempotencia.Preparar(idempotencyKey);
            if(chave is not null)
            {
                var docReplay=await FinanceiroIdempotencia.ConsultarReservarAsync(cn,nx,t,"conferencia.ajustar",chave,FinanceiroIdempotencia.Hash(new{ctx=new{t,e,x},documentoId,tipoDocumento=tipo,justificativa}),u,ct).ConfigureAwait(false);
                if(docReplay>0) return ConferenciaAjusteResultado.Replay;
            }

            // Lock da linha alvo (pai dos documentos comparados), no contexto t/e/x
            var registro=await cn.QueryFirstOrDefaultAsync<(long Id,decimal Valor)>(new CommandDefinition(
                $@"select id,{alvo.Coluna} as Valor from sigov.{alvo.Tabela} where tenant_id=@t and entidade_id=@e and exercicio_id=@x and id=@id and is_deleted=false for update",
                new{t,e,x,id=documentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            if(registro.Id==0) throw new InvalidOperationException("Documento não localizado para ajuste de conferência.");

            // Recálculo dentro da transação: soma dos documentos vigentes
            var calculado=await cn.ExecuteScalarAsync<decimal>(new CommandDefinition(alvo.SqlCalculado,new{id=documentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
            var deltaAtual=registro.Valor-calculado;
            if(deltaAtual!=diferencaPrevista) throw new InvalidOperationException($"409: A diferença do documento mudou desde a prévia (prévia: {diferencaPrevista:F2}; atual: {deltaAtual:F2}). Refaça a prévia.");
            if(deltaAtual==0m)
            {
                await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"conferencia.ajustar",chave,documentoId,ct).ConfigureAwait(false);
                nx.Commit();
                return ConferenciaAjusteResultado.SemAlteracao;
            }

            var antes=registro.Valor;
            var depois=registro.Valor-deltaAtual;
            await cn.ExecuteAsync(new CommandDefinition(
                $@"update sigov.{alvo.Tabela} set {alvo.Coluna}={alvo.Coluna}-@Delta,updated_at=now(),updated_by=@u where id=@id and tenant_id=@t",
                new{Delta=deltaAtual,id=documentoId,u,t},nx,cancellationToken:ct)).ConfigureAwait(false);

            // Empenho: rederivar status apos o ajuste
            if(alvo.Tabela=="empenho")
            {
                var emp=await cn.QueryFirstAsync<(decimal ValorTotal,decimal ValorAnulado,decimal ValorLiquidado,decimal ValorPago)>(new CommandDefinition(
                    @"select valor_total,valor_anulado,valor_liquidado,valor_pago from sigov.empenho where id=@id",
                    new{id=documentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
                var novoStatus=FinanceiroInvariantes.DerivarStatusEmpenho(emp.ValorTotal,emp.ValorAnulado,emp.ValorLiquidado,emp.ValorPago);
                await cn.ExecuteAsync(new CommandDefinition(
                    @"update sigov.empenho set status=@status,updated_at=now(),updated_by=@u where id=@id",
                    new{status=novoStatus,id=documentoId,u},nx,cancellationToken:ct)).ConfigureAwait(false);
            }

            // Evento de auditoria com antes/depois, autor, justificativa e correlationId
            var correlationId=Guid.NewGuid();
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.fila_evento (tenant_id,entidade_id,exercicio_id,tipo_evento,payload,correlation_id,created_by) values (@t,@e,@x,@tipo,@payload::jsonb,@correlationId,@u)",
                new{t,e,x,tipo="AJUSTE_CONFERENCIA",correlationId,payload=JsonSerializer.Serialize(new{tipoDocumento=tipo,documentoId,antes,depois,diferenca=deltaAtual,justificativa,autor=u,correlationId}),u},nx,cancellationToken:ct)).ConfigureAwait(false);

            await FinanceiroIdempotencia.ConfirmarDocumentoAsync(cn,nx,t,"conferencia.ajustar",chave,documentoId,ct).ConfigureAwait(false);
            nx.Commit();
            return ConferenciaAjusteResultado.Aplicado;
        }
        catch (NpgsqlException npe) when(npe.SqlState is "40001" or "40P01") { nx.Rollback(); throw new InvalidOperationException(FinanceiroInvariantes.Concorrencia); }
        catch { nx.Rollback(); throw; }
    }

}

internal static class FinanceiroIdempotencia
{
    public static string? Preparar(string? chave)
    {
        if(chave is null) return null;
        chave=chave.Trim();
        if(chave.Length==0) return null;
        if(chave.Length>120) throw new ArgumentException("Chave de idempotência inválida: o comprimento máximo é 120 caracteres.");
        return chave;
    }

    public static string Hash(object payload)
    {
        var bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static async Task<long> ConsultarReservarAsync(NpgsqlConnection cn,NpgsqlTransaction nx,long t,string escopo,string chave,string hash,long? usuarioId,CancellationToken ct)
    {
        var row=await cn.QueryFirstOrDefaultAsync<(long Id,long? DocumentoId,string PayloadHash)>(new CommandDefinition(
            @"select id,documento_id,payload_hash from sigov.financeiro_idempotencia where tenant_id=@t and escopo=@escopo and chave=@chave for update",
            new{t,escopo,chave},nx,cancellationToken:ct)).ConfigureAwait(false);
        if(row.Id==0)
        {
            await cn.ExecuteAsync(new CommandDefinition(
                @"insert into sigov.financeiro_idempotencia (tenant_id,escopo,chave,payload_hash,created_by) values (@t,@escopo,@chave,@hash,@u) on conflict (tenant_id, escopo, chave) do nothing",
                new{t,escopo,chave,hash,u=usuarioId},nx,cancellationToken:ct)).ConfigureAwait(false);
            row=await cn.QueryFirstAsync<(long Id,long? DocumentoId,string PayloadHash)>(new CommandDefinition(
                @"select id,documento_id,payload_hash from sigov.financeiro_idempotencia where tenant_id=@t and escopo=@escopo and chave=@chave for update",
                new{t,escopo,chave},nx,cancellationToken:ct)).ConfigureAwait(false);
        }
        if(!string.Equals(row.PayloadHash,hash,StringComparison.Ordinal)) throw new FinanceiroIdempotenciaConflitoException("Conflito de idempotência: a mesma chave foi utilizada com dados diferentes para esta operação.");
        if(row.DocumentoId is not null && row.DocumentoId.Value>0) return row.DocumentoId.Value;
        return 0;
    }

    public static async Task ConfirmarDocumentoAsync(NpgsqlConnection cn,NpgsqlTransaction nx,long t,string escopo,string? chave,long documentoId,CancellationToken ct)
    {
        if(chave is null) return;
        await cn.ExecuteAsync(new CommandDefinition(
            @"update sigov.financeiro_idempotencia set documento_id=@documentoId where tenant_id=@t and escopo=@escopo and chave=@chave",
            new{t,escopo,chave,documentoId},nx,cancellationToken:ct)).ConfigureAwait(false);
    }
}
