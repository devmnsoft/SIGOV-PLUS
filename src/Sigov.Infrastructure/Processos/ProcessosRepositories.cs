using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Processos;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Repositories;

namespace Sigov.Infrastructure.Processos;

public sealed class ProcessosAuditService : IAuditService
{
    private readonly DapperContext _context; private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user; private readonly ICorrelationIdProvider _correlation;
    public ProcessosAuditService(DapperContext context, ICurrentTenant tenant, ICurrentUser user, ICorrelationIdProvider correlation) { _context = context; _tenant = tenant; _user = user; _correlation = correlation; }
    public async Task RegistrarAsync(string modulo, string acao, string tabela, string chave, object? anterior, object? novo, CancellationToken cancellationToken = default)
    {
        if (!_tenant.TenantId.HasValue) return;
        const string sql = "insert into sigov.trilha_auditoria (tenant_id, entidade_id, exercicio_id, usuario_id, tabela, registro_id, acao, valores_anteriores, valores_novos, correlation_id) values (@TenantId, @EntidadeId, @ExercicioId, @UsuarioId, @Tabela, @RegistroId, @Acao, cast(@Anterior as jsonb), cast(@Novo as jsonb), @CorrelationId);";
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = _tenant.TenantId.Value, _tenant.EntidadeId, _tenant.ExercicioId, UsuarioId = _user.UsuarioId, Tabela = tabela, RegistroId = chave, Acao = acao, Anterior = anterior is null ? null : System.Text.Json.JsonSerializer.Serialize(anterior), Novo = novo is null ? null : System.Text.Json.JsonSerializer.Serialize(novo), CorrelationId = _correlation.CorrelationId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
    }
}

public sealed class ProcessoSequencialRepository : BaseRepository, IProcessoSequencialService
{
    private readonly DapperContext _context;
    public ProcessoSequencialRepository(DapperContext context) => _context = context;
    public async Task<string> ProximoAsync(long tenantId, long? entidadeId, long? exercicioId, int ano, string chave, string prefixo, CancellationToken ct)
    {
        const string lockSql = "select pg_advisory_lock(hashtext(@TenantId::text || ':' || coalesce(@EntidadeId::text, '') || ':' || coalesce(@ExercicioId::text, '') || ':' || @Chave || ':' || @Ano::text));";
        const string insertSql = """
            insert into sigov.controle_sequencial (tenant_id, entidade_id, exercicio_id, chave, ano, ultimo_numero)
            select @TenantId, @EntidadeId, @ExercicioId, @Chave, @Ano, 0
            where not exists (
                select 1 from sigov.controle_sequencial
                where tenant_id = @TenantId
                  and entidade_id is not distinct from @EntidadeId
                  and exercicio_id is not distinct from @ExercicioId
                  and chave = @Chave
                  and ano = @Ano
            );
            """;
        const string updateSql = """
            update sigov.controle_sequencial
            set ultimo_numero = ultimo_numero + 1, updated_at = now()
            where tenant_id = @TenantId
              and entidade_id is not distinct from @EntidadeId
              and exercicio_id is not distinct from @ExercicioId
              and chave = @Chave
              and ano = @Ano
            returning ultimo_numero;
            """;
        var args = new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Chave = chave, Ano = ano };
        using var cn = _context.CreateConnection();
        cn.Open();
        await cn.ExecuteAsync(Command(lockSql, args, ct)).ConfigureAwait(false);
        try
        {
            await cn.ExecuteAsync(Command(insertSql, args, ct)).ConfigureAwait(false);
            var n = await cn.ExecuteScalarAsync<int>(Command(updateSql, args, ct)).ConfigureAwait(false);
            return $"{prefixo}-{ano:D4}-{n:D6}";
        }
        finally
        {
            await cn.ExecuteAsync(Command(lockSql.Replace("pg_advisory_lock", "pg_advisory_unlock", StringComparison.Ordinal), args, ct)).ConfigureAwait(false);
        }
    }
}
public sealed class TipoProcessoRepository : BaseRepository, ITipoProcessoRepository
{
    private readonly DapperContext _context; public TipoProcessoRepository(DapperContext context) => _context = context;
    public async Task<PagedResult<TipoProcessoResponse>> ListarAsync(long tenantId, long? entidadeId, int page, int pageSize, CancellationToken ct) { var safe = new PaginationQuery(page, pageSize); const string countSql = "select count(*) from sigov.tipo_processo where tenant_id = @TenantId and is_deleted = false;"; const string sql = "select id, nome, descricao, prazo_padrao_dias as PrazoPadraoDias, exige_interessado as ExigeInteressado, permite_sigilo as PermiteSigilo, ativo from sigov.tipo_processo where tenant_id = @TenantId and is_deleted = false order by nome limit @Limit offset @Offset;"; using var cn = _context.CreateConnection(); var total = await cn.ExecuteScalarAsync<long>(Command(countSql, new { TenantId = tenantId }, ct)).ConfigureAwait(false); var rows = await cn.QueryAsync<TipoProcessoResponse>(Command(sql, new { TenantId = tenantId, Limit = safe.SafePageSize, Offset = safe.Offset }, ct)).ConfigureAwait(false); return new PagedResult<TipoProcessoResponse>(rows.AsList(), safe.SafePage, safe.SafePageSize, total); }
    public async Task<TipoProcessoResponse?> ObterAsync(long tenantId, long id, CancellationToken ct) { const string sql = "select id, nome, descricao, prazo_padrao_dias as PrazoPadraoDias, exige_interessado as ExigeInteressado, permite_sigilo as PermiteSigilo, ativo from sigov.tipo_processo where tenant_id = @TenantId and id = @Id and is_deleted = false;"; using var cn = _context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<TipoProcessoResponse>(Command(sql, new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false); }
    public async Task<long> CriarAsync(long tenantId, long? entidadeId, CriarTipoProcessoRequest r, long? usuarioId, Guid correlationId, CancellationToken ct) { const string sql = "insert into sigov.tipo_processo (tenant_id, entidade_id, nome, descricao, prazo_padrao_dias, exige_interessado, permite_sigilo, created_by, correlation_id) values (@TenantId, @EntidadeId, @Nome, @Descricao, @PrazoPadraoDias, @ExigeInteressado, @PermiteSigilo, @UsuarioId, @CorrelationId) returning id;"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, r.Nome, r.Descricao, r.PrazoPadraoDias, r.ExigeInteressado, r.PermiteSigilo, UsuarioId = usuarioId, CorrelationId = correlationId }, ct)).ConfigureAwait(false); }
    public async Task AtualizarAsync(long tenantId, long id, AtualizarTipoProcessoRequest r, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.tipo_processo set nome=@Nome, descricao=@Descricao, prazo_padrao_dias=@PrazoPadraoDias, exige_interessado=@ExigeInteressado, permite_sigilo=@PermiteSigilo, ativo=@Ativo, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, r.Nome, r.Descricao, r.PrazoPadraoDias, r.ExigeInteressado, r.PermiteSigilo, r.Ativo, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.tipo_processo set is_deleted=true, ativo=false, deleted_at=now(), deleted_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
}

public sealed class ProcessoDigitalRepository : BaseRepository, IProcessoDigitalRepository
{
    private readonly DapperContext _context; public ProcessoDigitalRepository(DapperContext context) => _context = context;
    public async Task<PagedResult<ProcessoResumoResponse>> ListarAsync(long tenantId, long? entidadeId, long? exercicioId, ProcessoFiltro f, CancellationToken ct) { var p = new PaginationQuery(f.Page, f.PageSize); var where = "where p.tenant_id = @TenantId and p.is_deleted = false and (@Numero is null or p.numero ilike @Numero) and (@Assunto is null or p.assunto ilike @Assunto) and (@TipoProcessoId is null or p.tipo_processo_id = @TipoProcessoId) and (@Status is null or p.status = @Status) and (@Prioridade is null or p.prioridade = @Prioridade) and (@InteressadoPessoaId is null or p.interessado_pessoa_id = @InteressadoPessoaId) and (@UnidadeAtualId is null or p.unidade_atual_id = @UnidadeAtualId) and (@Sigiloso is null or p.sigiloso = @Sigiloso)"; var countSql = $"select count(*) from sigov.processo_digital p {where};"; var sql = $"select p.id, p.numero, p.assunto, tp.nome as TipoProcesso, pe.nome as Interessado, p.status, p.prioridade, p.data_abertura as DataAbertura, p.prazo_resposta_at as PrazoRespostaAt, p.sigiloso from sigov.processo_digital p join sigov.tipo_processo tp on tp.id=p.tipo_processo_id and tp.tenant_id=p.tenant_id left join sigov.pessoa pe on pe.id=p.interessado_pessoa_id and pe.tenant_id=p.tenant_id {where} order by p.data_abertura desc limit @Limit offset @Offset;"; var prm = new { TenantId = tenantId, Numero = Like(f.Numero), Assunto = Like(f.Assunto), f.TipoProcessoId, f.Status, f.Prioridade, f.InteressadoPessoaId, f.UnidadeAtualId, f.Sigiloso, Limit = p.SafePageSize, Offset = p.Offset }; using var cn = _context.CreateConnection(); var total = await cn.ExecuteScalarAsync<long>(Command(countSql, prm, ct)).ConfigureAwait(false); var rows = await cn.QueryAsync<ProcessoResumoResponse>(Command(sql, prm, ct)).ConfigureAwait(false); return new PagedResult<ProcessoResumoResponse>(rows.AsList(), p.SafePage, p.SafePageSize, total); }
    public async Task<ProcessoDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken ct) { const string sql = "select p.id, p.numero, p.assunto, p.descricao, tp.nome as TipoProcesso, pe.nome as Interessado, p.status, p.prioridade, p.data_abertura as DataAbertura, p.prazo_resposta_at as PrazoRespostaAt, p.sigiloso from sigov.processo_digital p join sigov.tipo_processo tp on tp.id=p.tipo_processo_id and tp.tenant_id=p.tenant_id left join sigov.pessoa pe on pe.id=p.interessado_pessoa_id and pe.tenant_id=p.tenant_id where p.tenant_id=@TenantId and p.id=@Id and p.is_deleted=false;"; using var cn = _context.CreateConnection(); var row = await cn.QuerySingleOrDefaultAsync<ProcessoDetalheRow>(Command(sql, new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false); if (row is null) return null; var mov = await cn.QueryAsync<ProcessoMovimentacaoResponse>(Command("select id, despacho, status_anterior as StatusAnterior, status_novo as StatusNovo, movimentado_at as MovimentadoAt from sigov.processo_movimentacao where tenant_id=@TenantId and processo_digital_id=@Id and is_deleted=false order by movimentado_at desc;", new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false); var par = await cn.QueryAsync<ProcessoParecerResponse>(Command("select id, titulo, texto, tipo_parecer as TipoParecer, sigiloso, parecer_at as ParecerAt from sigov.processo_parecer where tenant_id=@TenantId and processo_digital_id=@Id and is_deleted=false order by parecer_at desc;", new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false); return new ProcessoDetalheResponse(row.Id, row.Numero, row.Assunto, row.Descricao, row.TipoProcesso, row.Interessado, row.Status, row.Prioridade, row.DataAbertura, row.PrazoRespostaAt, row.Sigiloso, mov.AsList(), par.AsList()); }
    public async Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, int ano, CriarProcessoRequest r, long usuarioId, Guid correlationId, CancellationToken ct) { const string sql = "insert into sigov.processo_digital (tenant_id, entidade_id, exercicio_id, tipo_processo_id, numero, ano, assunto, descricao, interessado_pessoa_id, unidade_origem_id, unidade_atual_id, usuario_abertura_id, status, prioridade, sigiloso, prazo_resposta_at, created_by, correlation_id) values (@TenantId, @EntidadeId, @ExercicioId, @TipoProcessoId, @Numero, @Ano, @Assunto, @Descricao, @InteressadoPessoaId, @UnidadeOrigemId, @UnidadeOrigemId, @UsuarioId, 'ABERTO', @Prioridade, @Sigiloso, @PrazoRespostaAt, @UsuarioId, @CorrelationId) returning id;"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Numero = numero, Ano = ano, r.TipoProcessoId, r.Assunto, r.Descricao, r.InteressadoPessoaId, r.UnidadeOrigemId, UsuarioId = usuarioId, r.Prioridade, r.Sigiloso, r.PrazoRespostaAt, CorrelationId = correlationId }, ct)).ConfigureAwait(false); }
    public async Task AtualizarAsync(long tenantId, long id, AtualizarProcessoRequest r, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.processo_digital set tipo_processo_id=@TipoProcessoId, assunto=@Assunto, descricao=@Descricao, prioridade=@Prioridade, sigiloso=@Sigiloso, prazo_resposta_at=@PrazoRespostaAt, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and status not in ('ENCERRADO','CANCELADO');"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, r.TipoProcessoId, r.Assunto, r.Descricao, r.Prioridade, r.Sigiloso, r.PrazoRespostaAt, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task AlterarStatusAsync(long tenantId, long id, string status, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.processo_digital set status=@Status, data_encerramento=case when @Status in ('ENCERRADO','CANCELADO') then now() else data_encerramento end, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, Status = status, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.processo_digital set is_deleted=true, ativo=false, deleted_at=now(), deleted_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    private static string? Like(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"%{value}%";
    private sealed record ProcessoDetalheRow(long Id, string Numero, string Assunto, string? Descricao, string TipoProcesso, string? Interessado, string Status, string Prioridade, DateTimeOffset DataAbertura, DateTimeOffset? PrazoRespostaAt, bool Sigiloso);
}

public sealed class ProcessoMovimentacaoRepository : BaseRepository, IProcessoMovimentacaoRepository
{
    private readonly DapperContext _context; public ProcessoMovimentacaoRepository(DapperContext context) => _context = context;
    public async Task<long> CriarAsync(long tenantId, long processoId, MovimentarProcessoRequest r, long usuarioId, CancellationToken ct) { const string sql = "insert into sigov.processo_movimentacao (tenant_id, entidade_id, exercicio_id, processo_digital_id, unidade_origem_id, unidade_destino_id, usuario_origem_id, usuario_destino_id, despacho, status_anterior, status_novo, created_by) select tenant_id, entidade_id, exercicio_id, id, unidade_atual_id, @UnidadeDestinoId, @UsuarioId, @UsuarioDestinoId, @Despacho, status, coalesce(@StatusNovo, 'EM_TRAMITACAO'), @UsuarioId from sigov.processo_digital where tenant_id=@TenantId and id=@ProcessoId and is_deleted=false and status not in ('ENCERRADO','CANCELADO') returning id; update sigov.processo_digital set unidade_atual_id=@UnidadeDestinoId, status=coalesce(@StatusNovo, 'EM_TRAMITACAO'), updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@ProcessoId and is_deleted=false and status not in ('ENCERRADO','CANCELADO');"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, ProcessoId = processoId, r.UnidadeDestinoId, UsuarioId = usuarioId, r.UsuarioDestinoId, r.Despacho, r.StatusNovo }, ct)).ConfigureAwait(false); }
}

public sealed class ProcessoParecerRepository : BaseRepository, IProcessoParecerRepository
{
    private readonly DapperContext _context; public ProcessoParecerRepository(DapperContext context) => _context = context;
    public async Task<long> CriarAsync(long tenantId, long processoId, EmitirParecerRequest r, long usuarioId, CancellationToken ct) { const string sql = "insert into sigov.processo_parecer (tenant_id, entidade_id, exercicio_id, processo_digital_id, usuario_id, titulo, texto, tipo_parecer, sigiloso, created_by) select tenant_id, entidade_id, exercicio_id, id, @UsuarioId, @Titulo, @Texto, @TipoParecer, @Sigiloso, @UsuarioId from sigov.processo_digital where tenant_id=@TenantId and id=@ProcessoId and is_deleted=false returning id;"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, ProcessoId = processoId, UsuarioId = usuarioId, r.Titulo, r.Texto, r.TipoParecer, r.Sigiloso }, ct)).ConfigureAwait(false); }
}

public sealed class ProtocoloAtendimentoRepository : BaseRepository, IProtocoloAtendimentoRepository
{
    private readonly DapperContext _context; public ProtocoloAtendimentoRepository(DapperContext context) => _context = context;
    public async Task<PagedResult<ProtocoloResumoResponse>> ListarAsync(long tenantId, ProtocoloFiltro f, CancellationToken ct) { var p = new PaginationQuery(f.Page, f.PageSize); const string where = "where pr.tenant_id=@TenantId and pr.is_deleted=false and (@Numero is null or pr.numero ilike @Numero) and (@Status is null or pr.status=@Status) and (@PessoaId is null or pr.pessoa_id=@PessoaId)"; var sql = $"select pr.id, pr.numero, pr.assunto, pr.canal, pr.status, pe.nome as Pessoa, pr.aberto_at as AbertoAt from sigov.protocolo_atendimento pr left join sigov.pessoa pe on pe.id=pr.pessoa_id and pe.tenant_id=pr.tenant_id {where} order by pr.aberto_at desc limit @Limit offset @Offset;"; var count = $"select count(*) from sigov.protocolo_atendimento pr {where};"; var prm = new { TenantId = tenantId, Numero = Like(f.Numero), f.Status, f.PessoaId, Limit = p.SafePageSize, Offset = p.Offset }; using var cn = _context.CreateConnection(); var total = await cn.ExecuteScalarAsync<long>(Command(count, prm, ct)).ConfigureAwait(false); var rows = await cn.QueryAsync<ProtocoloResumoResponse>(Command(sql, prm, ct)).ConfigureAwait(false); return new PagedResult<ProtocoloResumoResponse>(rows.AsList(), p.SafePage, p.SafePageSize, total); }
    public async Task<ProtocoloDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken ct) { const string sql = "select pr.id, pr.numero, pr.assunto, pr.descricao, pr.canal, pr.status, pe.nome as Pessoa, pr.processo_digital_id as ProcessoDigitalId, pr.aberto_at as AbertoAt from sigov.protocolo_atendimento pr left join sigov.pessoa pe on pe.id=pr.pessoa_id and pe.tenant_id=pr.tenant_id where pr.tenant_id=@TenantId and pr.id=@Id and pr.is_deleted=false;"; using var cn = _context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<ProtocoloDetalheResponse>(Command(sql, new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false); }
    public async Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, CriarProtocoloRequest r, long? usuarioId, CancellationToken ct) { const string sql = "insert into sigov.protocolo_atendimento (tenant_id, entidade_id, exercicio_id, numero, pessoa_id, assunto, descricao, canal, status, usuario_responsavel_id, created_by) values (@TenantId, @EntidadeId, @ExercicioId, @Numero, @PessoaId, @Assunto, @Descricao, @Canal, 'ABERTO', @UsuarioResponsavelId, @UsuarioId) returning id;"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Numero = numero, r.PessoaId, r.Assunto, r.Descricao, r.Canal, r.UsuarioResponsavelId, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task AtualizarAsync(long tenantId, long id, CriarProtocoloRequest r, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.protocolo_atendimento set pessoa_id=@PessoaId, assunto=@Assunto, descricao=@Descricao, canal=@Canal, usuario_responsavel_id=@UsuarioResponsavelId, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and status not in ('ENCERRADO','CANCELADO','CONVERTIDO_PROCESSO');"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, r.PessoaId, r.Assunto, r.Descricao, r.Canal, r.UsuarioResponsavelId, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task VincularProcessoAsync(long tenantId, long id, long processoId, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.protocolo_atendimento set processo_digital_id=@ProcessoId, status='CONVERTIDO_PROCESSO', updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and processo_digital_id is null;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, ProcessoId = processoId, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task EncerrarAsync(long tenantId, long id, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.protocolo_atendimento set status='ENCERRADO', encerrado_at=now(), updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    private static string? Like(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"%{value}%";
}

public sealed class OuvidoriaRepository : BaseRepository, IOuvidoriaRepository
{
    private readonly DapperContext _context; public OuvidoriaRepository(DapperContext context) => _context = context;
    public async Task<PagedResult<OuvidoriaResumoResponse>> ListarAsync(long tenantId, OuvidoriaFiltro f, CancellationToken ct) { var p = new PaginationQuery(f.Page, f.PageSize); const string where = "where tenant_id=@TenantId and is_deleted=false and (@Numero is null or numero ilike @Numero) and (@Status is null or status=@Status) and (@TipoManifestacao is null or tipo_manifestacao=@TipoManifestacao)"; var sql = $"select id, numero, tipo_manifestacao as TipoManifestacao, assunto, status, anonima, sigilosa, created_at as CreatedAt from sigov.ouvidoria_manifestacao {where} order by created_at desc limit @Limit offset @Offset;"; var count = $"select count(*) from sigov.ouvidoria_manifestacao {where};"; var prm = new { TenantId = tenantId, Numero = Like(f.Numero), f.Status, f.TipoManifestacao, Limit = p.SafePageSize, Offset = p.Offset }; using var cn = _context.CreateConnection(); var total = await cn.ExecuteScalarAsync<long>(Command(count, prm, ct)).ConfigureAwait(false); var rows = await cn.QueryAsync<OuvidoriaResumoResponse>(Command(sql, prm, ct)).ConfigureAwait(false); return new PagedResult<OuvidoriaResumoResponse>(rows.AsList(), p.SafePage, p.SafePageSize, total); }
    public async Task<OuvidoriaDetalheResponse?> ObterAsync(long tenantId, long id, bool mascarar, CancellationToken ct) { const string sql = "select ou.id, ou.numero, ou.tipo_manifestacao as TipoManifestacao, ou.assunto, ou.descricao, ou.status, ou.anonima, ou.sigilosa, case when @Mascarar then case when ou.anonima then 'Anônima' else 'Pessoa protegida pela LGPD' end else pe.nome end as Pessoa, ou.resposta, ou.processo_digital_id as ProcessoDigitalId from sigov.ouvidoria_manifestacao ou left join sigov.pessoa pe on pe.id=ou.pessoa_id and pe.tenant_id=ou.tenant_id where ou.tenant_id=@TenantId and ou.id=@Id and ou.is_deleted=false;"; using var cn = _context.CreateConnection(); return await cn.QuerySingleOrDefaultAsync<OuvidoriaDetalheResponse>(Command(sql, new { TenantId = tenantId, Id = id, Mascarar = mascarar }, ct)).ConfigureAwait(false); }
    public async Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, CriarOuvidoriaRequest r, long? usuarioId, CancellationToken ct) { const string sql = "insert into sigov.ouvidoria_manifestacao (tenant_id, entidade_id, exercicio_id, numero, pessoa_id, tipo_manifestacao, assunto, descricao, status, anonima, sigilosa, created_by) values (@TenantId, @EntidadeId, @ExercicioId, @Numero, @PessoaId, @TipoManifestacao, @Assunto, @Descricao, 'RECEBIDA', @Anonima, @Sigilosa, @UsuarioId) returning id;"; using var cn = _context.CreateConnection(); return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, Numero = numero, PessoaId = r.Anonima ? null : r.PessoaId, r.TipoManifestacao, r.Assunto, r.Descricao, r.Anonima, r.Sigilosa, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task ResponderAsync(long tenantId, long id, ResponderOuvidoriaRequest r, long usuarioId, CancellationToken ct) { const string sql = "update sigov.ouvidoria_manifestacao set resposta=@Resposta, status='RESPONDIDA', respondido_at=now(), respondido_by=@UsuarioId, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, r.Resposta, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task VincularProcessoAsync(long tenantId, long id, long processoId, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.ouvidoria_manifestacao set processo_digital_id=@ProcessoId, status='ENCAMINHADA', updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and processo_digital_id is null;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, ProcessoId = processoId, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    public async Task ArquivarAsync(long tenantId, long id, long? usuarioId, CancellationToken ct) { const string sql = "update sigov.ouvidoria_manifestacao set status='ARQUIVADA', updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;"; using var cn = _context.CreateConnection(); await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false); }
    private static string? Like(string? value) => string.IsNullOrWhiteSpace(value) ? null : $"%{value}%";
}

public sealed class DiarioOficialRepository : BaseRepository, IDiarioOficialRepository
{
    private readonly DapperContext _context; public DiarioOficialRepository(DapperContext context) => _context = context;
    public async Task<PagedResult<DiarioPublicacaoResponse>> ListarAsync(long tenantId, DiarioFiltro f, CancellationToken ct)
    {
        var p = new PaginationQuery(f.Page, f.PageSize);
        const string where = "where tenant_id=@TenantId and is_deleted=false and (@Status is null or status=@Status) and (@Inicio is null or data_publicacao >= @Inicio) and (@Fim is null or data_publicacao <= @Fim)";
        var sql = $"select id, numero_edicao as NumeroEdicao, data_publicacao as DataPublicacao, titulo, descricao, status, publicado_at as PublicadoAt from sigov.diario_oficial_publicacao {where} order by data_publicacao desc limit @Limit offset @Offset;";
        var count = $"select count(*) from sigov.diario_oficial_publicacao {where};";
        var prm = new { TenantId = tenantId, f.Status, f.Inicio, f.Fim, Limit = p.SafePageSize, Offset = p.Offset };
        using var cn = _context.CreateConnection();
        var total = await cn.ExecuteScalarAsync<long>(Command(count, prm, ct)).ConfigureAwait(false);
        var rows = await cn.QueryAsync<DiarioRow>(Command(sql, prm, ct)).ConfigureAwait(false);
        return new PagedResult<DiarioPublicacaoResponse>(rows.Select(r => new DiarioPublicacaoResponse(r.Id, r.NumeroEdicao, r.DataPublicacao, r.Titulo, r.Descricao, r.Status, r.PublicadoAt, Array.Empty<AtoOficialResponse>())).ToArray(), p.SafePage, p.SafePageSize, total);
    }

    public async Task<DiarioPublicacaoResponse?> ObterAsync(long tenantId, long id, CancellationToken ct)
    {
        const string sql = "select id, numero_edicao as NumeroEdicao, data_publicacao as DataPublicacao, titulo, descricao, status, publicado_at as PublicadoAt from sigov.diario_oficial_publicacao where tenant_id=@TenantId and id=@Id and is_deleted=false;";
        using var cn = _context.CreateConnection();
        var row = await cn.QuerySingleOrDefaultAsync<DiarioRow>(Command(sql, new { TenantId = tenantId, Id = id }, ct)).ConfigureAwait(false);
        if (row is null) return null;
        var atos = await ListarAtosAsync(tenantId, id, ct).ConfigureAwait(false);
        return new DiarioPublicacaoResponse(row.Id, row.NumeroEdicao, row.DataPublicacao, row.Titulo, row.Descricao, row.Status, row.PublicadoAt, atos);
    }

    public async Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, CriarDiarioPublicacaoRequest r, long? usuarioId, CancellationToken ct)
    {
        const string sql = "insert into sigov.diario_oficial_publicacao (tenant_id, entidade_id, exercicio_id, numero_edicao, data_publicacao, titulo, descricao, status, created_by) values (@TenantId, @EntidadeId, @ExercicioId, @NumeroEdicao, @DataPublicacao, @Titulo, @Descricao, 'RASCUNHO', @UsuarioId) returning id;";
        using var cn = _context.CreateConnection();
        return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, EntidadeId = entidadeId, ExercicioId = exercicioId, r.NumeroEdicao, r.DataPublicacao, r.Titulo, r.Descricao, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task AtualizarAsync(long tenantId, long id, CriarDiarioPublicacaoRequest r, long? usuarioId, CancellationToken ct)
    {
        const string sql = "update sigov.diario_oficial_publicacao set numero_edicao=@NumeroEdicao, data_publicacao=@DataPublicacao, titulo=@Titulo, descricao=@Descricao, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false and status <> 'PUBLICADO';";
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, r.NumeroEdicao, r.DataPublicacao, r.Titulo, r.Descricao, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task PublicarAsync(long tenantId, long id, long usuarioId, CancellationToken ct)
    {
        const string sql = "update sigov.diario_oficial_publicacao set status='PUBLICADO', publicado_at=now(), publicado_by=@UsuarioId, updated_at=now(), updated_by=@UsuarioId where tenant_id=@TenantId and id=@Id and is_deleted=false;";
        using var cn = _context.CreateConnection();
        await cn.ExecuteAsync(Command(sql, new { TenantId = tenantId, Id = id, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<long> CriarAtoAsync(long tenantId, long publicacaoId, CriarAtoOficialRequest r, long? usuarioId, CancellationToken ct)
    {
        const string sql = "insert into sigov.ato_oficial (tenant_id, entidade_id, exercicio_id, diario_oficial_publicacao_id, tipo_ato, numero, titulo, texto, data_ato, origem, created_by) select tenant_id, entidade_id, exercicio_id, id, @TipoAto, @Numero, @Titulo, @Texto, @DataAto, @Origem, @UsuarioId from sigov.diario_oficial_publicacao where tenant_id=@TenantId and id=@PublicacaoId and is_deleted=false returning id;";
        using var cn = _context.CreateConnection();
        return await cn.ExecuteScalarAsync<long>(Command(sql, new { TenantId = tenantId, PublicacaoId = publicacaoId, r.TipoAto, r.Numero, r.Titulo, r.Texto, r.DataAto, r.Origem, UsuarioId = usuarioId }, ct)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyCollection<AtoOficialResponse>> ListarAtosAsync(long tenantId, long publicacaoId, CancellationToken ct)
    {
        const string sql = "select id, diario_oficial_publicacao_id as DiarioOficialPublicacaoId, tipo_ato as TipoAto, numero, titulo, texto, data_ato as DataAto, origem from sigov.ato_oficial where tenant_id=@TenantId and diario_oficial_publicacao_id=@PublicacaoId and is_deleted=false order by id;";
        using var cn = _context.CreateConnection();
        var rows = await cn.QueryAsync<AtoOficialResponse>(Command(sql, new { TenantId = tenantId, PublicacaoId = publicacaoId }, ct)).ConfigureAwait(false);
        return rows.AsList();
    }

    private sealed record DiarioRow(long Id, string NumeroEdicao, DateOnly DataPublicacao, string Titulo, string? Descricao, string Status, DateTimeOffset? PublicadoAt);
}
