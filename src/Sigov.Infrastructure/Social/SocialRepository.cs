using System.Text;
using Sigov.Application.Common;
using Sigov.Application.Social;

namespace Sigov.Infrastructure.Social;

public sealed class SocialRepository : ISocialUnidadeRepository, ISocialFamiliaRepository, ISocialPessoaRepository, ISocialCadastroRepository, ISocialProgramaRepository, ISocialBeneficioRepository, ISocialAtendimentoRepository, ISocialVisitaRepository, ISocialParecerRepository, ISocialAcompanhamentoRepository, ISocialVigilanciaRepository, ISocialDashboardRepository, ISocialExportacaoRepository, ISocialSequencialService
{
    private static long _id = 1;
    private static long Next() => Interlocked.Increment(ref _id);

    Task<PagedResult<SocialUnidadeResponse>> ISocialUnidadeRepository.ListarAsync(long t, long e, SocialUnidadeFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialUnidadeResponse>.Empty(f.Page, f.PageSize));
    Task<SocialUnidadeResponse?> ISocialUnidadeRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialUnidadeResponse?>(new(id, "***", "Unidade Social", "CRAS", "ATIVA", true));
    Task<long> ISocialUnidadeRepository.CriarAsync(SocialContexto c, SocialUnidadeCreateRequest r, CancellationToken ct) => Task.FromResult(Next());
    Task ISocialUnidadeRepository.AtualizarAsync(SocialContexto c, long id, SocialUnidadeUpdateRequest r, CancellationToken ct) => Task.CompletedTask;
    Task ISocialUnidadeRepository.ExcluirAsync(SocialContexto c, long id, CancellationToken ct) => Task.CompletedTask;

    Task<PagedResult<SocialFamiliaResponse>> ISocialFamiliaRepository.ListarAsync(long t, long e, SocialFamiliaFiltro f, CancellationToken ct) => Task.FromResult(PagedResult<SocialFamiliaResponse>.Empty(f.Page, f.PageSize));
    Task<SocialFamiliaResponse?> ISocialFamiliaRepository.ObterAsync(long t, long e, long id, CancellationToken ct) => Task.FromResult<SocialFamiliaResponse?>(new(id, "FAM-****", null, null, null, 0, "ATIVA", "***"));
    Task<long> ISocialFamiliaRepository.CriarAsync(SocialContexto c, SocialFamiliaCreateRequest r, string codigo, CancellationToken ct) => Task.FromResult(Next());
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

    Task<SocialDashboardResponse> ISocialDashboardRepository.ObterAsync(long t, long e, CancellationToken ct) => Task.FromResult(new SocialDashboardResponse(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), Array.Empty<object>(), new[] { "Assistência Social base carregada." }));

    Task<byte[]> ISocialExportacaoRepository.ExportarAsync(long t, long e, string recurso, string formato, CancellationToken ct) => Task.FromResult(Encoding.UTF8.GetBytes(formato.Equals("json", StringComparison.OrdinalIgnoreCase) ? "[]" : "dados_mascarados\n"));

    Task<string> ISocialSequencialService.ProximoAsync(long tenantId, long entidadeId, string prefixo, CancellationToken ct) => Task.FromResult($"{prefixo}-{DateTime.UtcNow.Year}-000001");
}
