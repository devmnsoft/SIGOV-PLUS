using System.Text;
using Dapper;
using Sigov.Application.Common;
using Sigov.Application.Social;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Infrastructure.Social;

public sealed class SocialRepository : ISocialUnidadeRepository, ISocialFamiliaRepository, ISocialPessoaRepository, ISocialCadastroRepository, ISocialProgramaRepository, ISocialBeneficioRepository, ISocialAtendimentoRepository, ISocialVisitaRepository, ISocialParecerRepository, ISocialAcompanhamentoRepository, ISocialVigilanciaRepository, ISocialDashboardRepository, ISocialExportacaoRepository, ISocialSequencialService
{
    private readonly DapperContext _context;
    public SocialRepository(DapperContext context) => _context = context;
    private static CommandDefinition Cmd(string sql, object? p, CancellationToken ct) => new(sql, p, cancellationToken: ct);
    private static long _id; private static long Next()=>Interlocked.Increment(ref _id);
    private sealed record DashboardRow(long TotalFamilias,long FamiliasRisco,long Pessoas,long Atendimentos,long Visitas,long Beneficios,long Pendentes,long Vulnerabilidades,long Acompanhamentos,long Encaminhamentos,long Ocorrencias);
    private async Task<PagedResult<T>> Page<T>(string sql, object args, int page, int size, CancellationToken ct) { using var db=_context.CreateConnection(); using var m=await db.QueryMultipleAsync(Cmd(sql,args,ct)); var rows=(await m.ReadAsync<T>()).AsList(); var total=await m.ReadFirstAsync<long>(); return new(rows,page,size,total); }

    Task<PagedResult<SocialUnidadeResponse>> ISocialUnidadeRepository.ListarAsync(long t, long e, SocialUnidadeFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialUnidadeResponse>.Empty(f.Page, f.PageSize));
    Task<SocialUnidadeResponse?> ISocialUnidadeRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialUnidadeResponse?>(new(id, "***", "Unidade Social", "CRAS", "ATIVA", true));
    Task<long> ISocialUnidadeRepository.CriarAsync(SocialContexto c, SocialUnidadeCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialUnidadeRepository.AtualizarAsync(SocialContexto c, long id, SocialUnidadeUpdateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialUnidadeRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;

    async Task<PagedResult<SocialFamiliaResponse>> ISocialFamiliaRepository.ListarAsync(long t,long e,SocialFamiliaFiltro f,CancellationToken ct) { const string q="select id,codigo_familia CodigoFamilia,nis_familiar NisFamiliar,responsavel_pessoa_id ResponsavelPessoaId,renda_familiar RendaFamiliar,quantidade_membros QuantidadeMembros,situacao,classificacao_risco ClassificacaoRisco from sigov.social_familia where tenant_id=@t and entidade_id=@e and not is_deleted and (@termo is null or codigo_familia ilike '%'||@termo||'%') order by id desc limit @size offset @off; select count(*) from sigov.social_familia where tenant_id=@t and entidade_id=@e and not is_deleted and (@termo is null or codigo_familia ilike '%'||@termo||'%');"; return await Page<SocialFamiliaResponse>(q,new{t,e,termo=f.Termo,size=f.PageSize,off=(f.Page-1)*f.PageSize},f.Page,f.PageSize,ct); }
    Task<SocialFamiliaResponse?> ISocialFamiliaRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialFamiliaResponse?>(new(id, "FAM-****", null, null, null, 0, "ATIVA", "***"));
    async Task<long> ISocialFamiliaRepository.CriarAsync(SocialContexto c,SocialFamiliaCreateRequest r,string codigo,CancellationToken ct) { using var db=_context.CreateConnection(); return await db.ExecuteScalarAsync<long>(Cmd("insert into sigov.social_familia(tenant_id,entidade_id,codigo_familia,nis_familiar,responsavel_pessoa_id,renda_familiar,situacao,classificacao_risco,latitude,longitude,observacao,created_by) values(@TenantId,@EntidadeId,@codigo,@NisFamiliar,@ResponsavelPessoaId,@RendaFamiliar,@Situacao,@ClassificacaoRisco,@Latitude,@Longitude,@Observacao,@UsuarioId) returning id",new{c.TenantId,c.EntidadeId,c.UsuarioId,codigo,r.NisFamiliar,r.ResponsavelPessoaId,r.RendaFamiliar,r.Situacao,r.ClassificacaoRisco,r.Latitude,r.Longitude,r.Observacao},ct)); }
    Task ISocialFamiliaRepository.AtualizarAsync(SocialContexto c, long id, SocialFamiliaUpdateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialFamiliaRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;
    Task<long> ISocialFamiliaRepository.AdicionarComposicaoAsync(SocialContexto c, long familiaId, SocialComposicaoFamiliarRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task<long> ISocialFamiliaRepository.RegistrarVulnerabilidadeAsync(SocialContexto c, long familiaId, SocialVulnerabilidadeCreateRequest r, CancellationToken ct) => Task.FromResult(Next());

    Task<PagedResult<SocialPessoaResponse>> ISocialPessoaRepository.ListarAsync(long t, long e, SocialPessoaFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialPessoaResponse>.Empty(f.Page, f.PageSize));
    Task<SocialPessoaResponse?> ISocialPessoaRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialPessoaResponse?>(new(id, 1, null, "***", null, "ATIVA"));
    Task<long> ISocialPessoaRepository.CriarAsync(SocialContexto c, SocialPessoaCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialPessoaRepository.AtualizarAsync(SocialContexto c, long id, SocialPessoaUpdateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialPessoaRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;

    Task<long> ISocialCadastroRepository.CriarAsync(SocialContexto c, SocialCadastroCreateRequest r, string numero, CancellationToken ct) => Task.FromResult(Next());

    Task<PagedResult<SocialProgramaResponse>> ISocialProgramaRepository.ListarAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialProgramaResponse>.Empty(f.Page, f.PageSize));
    Task<long> ISocialProgramaRepository.CriarAsync(SocialContexto c, SocialProgramaCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialProgramaRepository.AtualizarAsync(SocialContexto c, long id, SocialProgramaCreateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialProgramaRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;

    Task<PagedResult<SocialBeneficioResponse>> ISocialBeneficioRepository.ListarAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialBeneficioResponse>.Empty(f.Page, f.PageSize));
    Task<long> ISocialBeneficioRepository.CriarAsync(SocialContexto c, SocialBeneficioCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialBeneficioRepository.AtualizarAsync(SocialContexto c, long id, SocialBeneficioCreateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialBeneficioRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;
    Task<PagedResult<SocialBeneficioConcessaoResponse>> ISocialBeneficioRepository.ListarConcessoesAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialBeneficioConcessaoResponse>.Empty(f.Page, f.PageSize));
    Task<long> ISocialBeneficioRepository.ConcederAsync(SocialContexto c, SocialBeneficioConcessaoCreateRequest r, string numero, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialBeneficioRepository.AlterarStatusConcessaoAsync(SocialContexto c, long id, string status, CancellationToken ct) => Task.CompletedTask;

    Task<PagedResult<SocialAtendimentoResponse>> ISocialAtendimentoRepository.ListarAsync(long t, long e, SocialAtendimentoFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialAtendimentoResponse>.Empty(f.Page, f.PageSize));
    Task<SocialAtendimentoResponse?> ISocialAtendimentoRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialAtendimentoResponse?>(new(id, "ATSOC-****", null, null, null, DateTimeOffset.UtcNow, "ACOLHIDA", "ABERTO"));
    Task<long> ISocialAtendimentoRepository.CriarAsync(SocialContexto c, SocialAtendimentoCreateRequest r, string numero, CancellationToken ct) => Task.FromResult(Next());
    Task<long> ISocialAtendimentoRepository.EncaminharAsync(SocialContexto c, long atendimentoId, SocialEncaminhamentoCreateRequest r, CancellationToken ct) => Task.FromResult(Next());

    Task<PagedResult<SocialVisitaResponse>> ISocialVisitaRepository.ListarAsync(long t, long e, SocialVisitaFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialVisitaResponse>.Empty(f.Page, f.PageSize));
    Task<SocialVisitaResponse?> ISocialVisitaRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialVisitaResponse?>(new(id, null, null, DateTimeOffset.UtcNow, "ACOMPANHAMENTO", "REALIZADA"));
    Task<long> ISocialVisitaRepository.CriarAsync(SocialContexto c, SocialVisitaCreateRequest r, CancellationToken ct) => Task.FromResult(Next());

    Task<PagedResult<SocialParecerResponse>> ISocialParecerRepository.ListarAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialParecerResponse>.Empty(f.Page, f.PageSize));
    Task<long> ISocialParecerRepository.CriarAsync(SocialContexto c, SocialParecerCreateRequest r, CancellationToken ct) => Task.FromResult(Next());

    Task<PagedResult<SocialAcompanhamentoResponse>> ISocialAcompanhamentoRepository.ListarAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialAcompanhamentoResponse>.Empty(f.Page, f.PageSize));
    Task<long> ISocialAcompanhamentoRepository.CriarAsync(SocialContexto c, SocialAcompanhamentoCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialAcompanhamentoRepository.EncerrarAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;

    Task<PagedResult<object>> ISocialVigilanciaRepository.ListarIndicadoresAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<object>.Empty(f.Page, f.PageSize));
    Task<long> ISocialVigilanciaRepository.CriarIndicadorAsync(SocialContexto c, SocialVigilanciaIndicadorCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task<PagedResult<object>> ISocialVigilanciaRepository.ListarOcorrenciasAsync(long t, long e, SocialFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<object>.Empty(f.Page, f.PageSize));
    Task<long> ISocialVigilanciaRepository.CriarOcorrenciaAsync(SocialContexto c, SocialVigilanciaOcorrenciaCreateRequest r, CancellationToken ct) => Task.FromResult(Next());

    async Task<SocialDashboardResponse> ISocialDashboardRepository.ObterAsync(long t,long e,CancellationToken ct) { const string q=@"select
(select count(*) from sigov.social_familia where tenant_id=@t and entidade_id=@e and not is_deleted) TotalFamilias,
(select count(*) from sigov.social_familia where tenant_id=@t and entidade_id=@e and classificacao_risco in ('ALTO','CRITICO') and not is_deleted) FamiliasRisco,
(select count(*) from sigov.social_pessoa where tenant_id=@t and entidade_id=@e and not is_deleted) Pessoas,
(select count(*) from sigov.social_atendimento where tenant_id=@t and entidade_id=@e and data_atendimento>=date_trunc('month',now()) and not is_deleted) Atendimentos,
(select count(*) from sigov.social_visita where tenant_id=@t and entidade_id=@e and data_visita>=date_trunc('month',now()) and not is_deleted) Visitas,
(select count(*) from sigov.social_beneficio_concessao where tenant_id=@t and entidade_id=@e and status in ('CONCEDIDO','ENTREGUE') and data_concessao>=date_trunc('month',now()) and not is_deleted) Beneficios,
(select count(*) from sigov.social_beneficio_solicitacao where tenant_id=@t and entidade_id=@e and status in ('SOLICITADA','EM_ANALISE') and not is_deleted) Pendentes,
(select count(*) from sigov.social_vulnerabilidade where tenant_id=@t and entidade_id=@e and status in ('ABERTA','EM_ACOMPANHAMENTO') and not is_deleted) Vulnerabilidades,
(select count(*) from sigov.social_acompanhamento_familiar where tenant_id=@t and entidade_id=@e and status='ATIVO' and not is_deleted) Acompanhamentos,
(select count(*) from sigov.social_encaminhamento where tenant_id=@t and entidade_id=@e and status in ('PENDENTE','ENVIADO') and not is_deleted) Encaminhamentos,
(select count(*) from sigov.social_vigilancia_ocorrencia where tenant_id=@t and entidade_id=@e and data_ocorrencia>=date_trunc('month',now()) and not is_deleted) Ocorrencias;"; using var db=_context.CreateConnection(); var x=await db.QuerySingleAsync<DashboardRow>(Cmd(q,new{t,e},ct)); return new(x.TotalFamilias,x.FamiliasRisco,x.Pessoas,x.Atendimentos,x.Visitas,x.Beneficios,x.Pendentes,x.Vulnerabilidades,x.Acompanhamentos,x.Encaminhamentos,x.Ocorrencias,[],[],[],[],[],[]); }

    Task<byte[]> ISocialExportacaoRepository.ExportarAsync(long t, long e, string recurso, string formato, CancellationToken ct) => Task.FromResult(Encoding.UTF8.GetBytes(formato.Equals("json", StringComparison.OrdinalIgnoreCase) ? "[]" : "dados_mascarados\n"));

    Task<string> ISocialSequencialService.ProximoAsync(long tenantId, long entidadeId, string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-{DateTime.UtcNow.Year}-000001");
}
