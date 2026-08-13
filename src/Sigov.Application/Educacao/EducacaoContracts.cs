using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Educacao;

public static class EducacaoPermissoes
{
    public const string Modulo = "educacao";
    public const string Exportar = "educacao.exportar";
}

public sealed record EscolaCreateRequest(string Codigo, string Nome, string TipoEscola = "MUNICIPAL", string Situacao = "ATIVA", string? InepCodigo = null, string? Observacao = null);
public sealed record EscolaUpdateRequest(string Codigo, string Nome, string TipoEscola = "MUNICIPAL", string Situacao = "ATIVA", string? InepCodigo = null, string? Observacao = null);
public sealed record EscolaFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null);
public sealed record EscolaResponse(long Id, string Codigo, string Nome, string TipoEscola, string Situacao, string? InepCodigo, bool Ativo);

public sealed record AnoLetivoCreateRequest(int Ano, DateOnly DataInicio, DateOnly DataFim, string Status = "PLANEJADO", long? EscolaId = null, string? Observacao = null);
public sealed record AnoLetivoResponse(long Id, int Ano, DateOnly DataInicio, DateOnly DataFim, string Status, long? EscolaId);

public sealed record CursoCreateRequest(string Codigo, string Nome, string EtapaEnsino, string? Modalidade = null);
public sealed record SerieAnoCreateRequest(string Codigo, string Nome, int Ordem);
public sealed record CursoResponse(long Id, string Codigo, string Nome, string EtapaEnsino, string? Modalidade);
public sealed record SerieAnoResponse(long Id, long CursoId, string Codigo, string Nome, int Ordem);

public sealed record TurmaCreateRequest(long EscolaId, long AnoLetivoId, long CursoId, long SerieAnoId, string Codigo, string Nome, string Turno, int Capacidade, string Status = "PLANEJADA");
public sealed record TurmaUpdateRequest(string Codigo, string Nome, string Turno, int Capacidade, string Status = "PLANEJADA");
public sealed record TurmaFiltro(int Page = 1, int PageSize = 20, long? EscolaId = null, string? Status = null, string? Termo = null);
public sealed record TurmaResponse(long Id, long EscolaId, long AnoLetivoId, string Codigo, string Nome, string Turno, int Capacidade, int VagasOcupadas, string Status);

public sealed record AlunoCreateRequest(long PessoaId, string CodigoAluno, string? Nis = null, string? CartaoSus = null, bool NecessidadeEspecial = false, Dictionary<string, object?>? DadosSensiveis = null, string Situacao = "ATIVO");
public sealed record AlunoUpdateRequest(string CodigoAluno, string? Nis = null, string? CartaoSus = null, bool NecessidadeEspecial = false, Dictionary<string, object?>? DadosSensiveis = null, string Situacao = "ATIVO");
public sealed record AlunoFiltro(int Page = 1, int PageSize = 20, string? Termo = null, string? Situacao = null);
public sealed record AlunoResumoResponse(long Id, long PessoaId, string CodigoAluno, string? Nis, string? CartaoSus, bool NecessidadeEspecial, string Situacao);
public sealed record AlunoDetalheResponse(long Id, long PessoaId, string CodigoAluno, string? Nis, string? CartaoSus, bool NecessidadeEspecial, string Situacao, IReadOnlyCollection<ResponsavelAlunoResponse> Responsaveis);

public sealed record ResponsavelAlunoRequest(long PessoaId, string Parentesco, bool ResponsavelLegal = false, bool Financeiro = false, bool AutorizadoBuscar = false, bool ContatoEmergencia = false);
public sealed record ResponsavelAlunoResponse(long Id, long AlunoId, long PessoaId, string Parentesco, bool ResponsavelLegal, bool Financeiro, bool AutorizadoBuscar, bool ContatoEmergencia);

public sealed record MatriculaCreateRequest(long AlunoId, long EscolaId, long AnoLetivoId, long TurmaId, string? NumeroMatricula = null, DateOnly? DataMatricula = null, string Status = "ATIVA", string? Origem = null, string? Observacao = null);
public sealed record MatriculaUpdateRequest(string Status, string? Observacao = null);
public sealed record MatriculaFiltro(int Page = 1, int PageSize = 20, long? AlunoId = null, long? TurmaId = null, string? Status = null);
public sealed record MatriculaResponse(long Id, long AlunoId, long EscolaId, long AnoLetivoId, long TurmaId, string NumeroMatricula, DateOnly DataMatricula, string Status);
public sealed record CancelarMatriculaRequest(string? Motivo = null);
public sealed record TransferirMatriculaRequest(long NovaTurmaId, string? Motivo = null);

public sealed record ProfessorCreateRequest(long PessoaId, string CodigoProfessor, long? ServidorId = null, string? Formacao = null, string Situacao = "ATIVO");
public sealed record ProfessorTurmaRequest(long TurmaId, string ComponenteCurricular, decimal? CargaHorariaSemanal = null);
public sealed record ProfessorResponse(long Id, long PessoaId, string CodigoProfessor, string? Formacao, string Situacao);

public sealed record FrequenciaCreateRequest(long TurmaId, long AlunoId, long? ProfessorId, DateOnly DataAula, string? ComponenteCurricular, bool Presente = true, string? Justificativa = null);
public sealed record FrequenciaFiltro(int Page = 1, int PageSize = 20, long? TurmaId = null, long? AlunoId = null, DateOnly? Inicio = null, DateOnly? Fim = null);
public sealed record FrequenciaResponse(long Id, long TurmaId, long AlunoId, DateOnly DataAula, string? ComponenteCurricular, bool Presente);

public sealed record AvaliacaoCreateRequest(long TurmaId, long? ProfessorId, string ComponenteCurricular, string Titulo, DateOnly DataAvaliacao, decimal ValorMaximo = 10m, decimal Peso = 1m, string Status = "ABERTA");
public sealed record NotaCreateRequest(long AlunoId, decimal Valor, string? Observacao = null);
public sealed record AvaliacaoResponse(long Id, long TurmaId, string ComponenteCurricular, string Titulo, DateOnly DataAvaliacao, decimal ValorMaximo, decimal Peso, string Status);
public sealed record NotaResponse(long Id, long AvaliacaoId, long AlunoId, decimal Valor, string? Observacao);
public sealed record BoletimItemResponse(string ComponenteCurricular, string Avaliacao, DateOnly DataAvaliacao, decimal ValorMaximo, decimal? Nota, string Situacao);
public sealed record BoletimResponse(long AlunoId, decimal MediaGeral, IReadOnlyCollection<BoletimItemResponse> Itens);

public sealed record PreMatriculaCreateRequest(long AlunoPessoaId, long? ResponsavelPessoaId, long? EscolaPreferencialId, int AnoLetivo, string EtapaEnsino, string? Protocolo = null, string Status = "RECEBIDA", decimal? Pontuacao = null, string? Observacao = null);
public sealed record PreMatriculaFiltro(int Page = 1, int PageSize = 20, string? Status = null, string? Protocolo = null);
public sealed record PreMatriculaResponse(long Id, string Protocolo, long AlunoPessoaId, int AnoLetivo, string EtapaEnsino, string Status, decimal? Pontuacao);
public sealed record ConverterPreMatriculaRequest(long EscolaId, long AnoLetivoId, long TurmaId, string? NumeroMatricula = null);

public sealed record EducacensoRegistroRequest(string TipoRegistro, string Status = "PENDENTE", Dictionary<string, object?>? Payload = null, long? EscolaId = null, long? AlunoId = null, long? TurmaId = null);
public sealed record EducacensoRegistroResponse(long Id, string TipoRegistro, string Status, Dictionary<string, object?> Payload, string? Erro);

public sealed record EducacaoDashboardResponse(long TotalEscolas, long TotalAlunosAtivos, long TotalMatriculasAtivas, long TotalTurmasAbertas, long VagasTotais, long VagasOcupadas, long PreMatriculasPendentes, decimal FrequenciaMediaMes, long AvaliacoesAbertas, long RegistrosEducacensoPendentes, IReadOnlyCollection<object> AlunosPorEscola, IReadOnlyCollection<object> MatriculasPorStatus, IReadOnlyCollection<object> FrequenciaResumo, IReadOnlyCollection<object> UltimasMatriculas, IReadOnlyCollection<string> Alertas);

public interface IEducacaoRepository
{
    Task<PagedResult<T>> ListarAsync<T>(long tenantId, long entidadeId, string recurso, object filtro, CancellationToken ct);
    Task<T?> ObterAsync<T>(long tenantId, long entidadeId, string recurso, long id, CancellationToken ct);
    Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, object request, long? usuarioId, CancellationToken ct);
    Task AtualizarAsync(long tenantId, long entidadeId, string recurso, long id, object request, long? usuarioId, CancellationToken ct);
    Task ExcluirAsync(long tenantId, long entidadeId, string recurso, long id, long? usuarioId, CancellationToken ct);
    Task<EducacaoDashboardResponse> DashboardAsync(long tenantId, long entidadeId, CancellationToken ct);
    Task<byte[]> ExportarAsync(long tenantId, long entidadeId, string recurso, string formato, CancellationToken ct);
    Task<BoletimResponse> ObterBoletimAsync(long tenantId, long entidadeId, long alunoId, CancellationToken ct);
}

public interface IEscolaRepository : IEducacaoRepository { }
public interface IAnoLetivoRepository : IEducacaoRepository { }
public interface ICursoRepository : IEducacaoRepository { }
public interface ITurmaRepository : IEducacaoRepository { }
public interface IAlunoRepository : IEducacaoRepository { }
public interface IMatriculaRepository : IEducacaoRepository { }
public interface IProfessorRepository : IEducacaoRepository { }
public interface IFrequenciaRepository : IEducacaoRepository { }
public interface IAvaliacaoRepository : IEducacaoRepository { }
public interface IPreMatriculaRepository : IEducacaoRepository { }
public interface IEducacensoRepository : IEducacaoRepository { }
public interface IEducacaoDashboardRepository : IEducacaoRepository { }
public interface IEducacaoExportacaoRepository : IEducacaoRepository { }
public interface IEducacaoSequencialService { Task<string> ProximoAsync(string prefixo, int ano, CancellationToken ct); }

public interface IEscolaService { Task<Result<PagedResult<EscolaResponse>>> ListarAsync(EscolaFiltro filtro, CancellationToken ct); Task<Result<EscolaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(EscolaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, EscolaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IAnoLetivoService { Task<Result<PagedResult<AnoLetivoResponse>>> ListarAsync(EscolaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(AnoLetivoCreateRequest request, CancellationToken ct); Task<Result> EncerrarAsync(long id, CancellationToken ct); }
public interface ICursoService { Task<Result<PagedResult<CursoResponse>>> ListarAsync(EscolaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(CursoCreateRequest request, CancellationToken ct); Task<Result<long>> CriarSerieAsync(long cursoId, SerieAnoCreateRequest request, CancellationToken ct); }
public interface ITurmaService { Task<Result<PagedResult<TurmaResponse>>> ListarAsync(TurmaFiltro filtro, CancellationToken ct); Task<Result<TurmaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(TurmaCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, TurmaUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IAlunoService { Task<Result<PagedResult<AlunoResumoResponse>>> ListarAsync(AlunoFiltro filtro, CancellationToken ct); Task<Result<AlunoDetalheResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(AlunoCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, AlunoUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); Task<Result<long>> AdicionarResponsavelAsync(long alunoId, ResponsavelAlunoRequest request, CancellationToken ct); }
public interface IMatriculaService { Task<Result<PagedResult<MatriculaResponse>>> ListarAsync(MatriculaFiltro filtro, CancellationToken ct); Task<Result<MatriculaResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(MatriculaCreateRequest request, CancellationToken ct); Task<Result> CancelarAsync(long id, CancelarMatriculaRequest request, CancellationToken ct); Task<Result> TransferirAsync(long id, TransferirMatriculaRequest request, CancellationToken ct); }
public interface IProfessorService { Task<Result<PagedResult<ProfessorResponse>>> ListarAsync(EscolaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(ProfessorCreateRequest request, CancellationToken ct); Task<Result<long>> VincularTurmaAsync(long professorId, ProfessorTurmaRequest request, CancellationToken ct); }
public interface IFrequenciaService { Task<Result<PagedResult<FrequenciaResponse>>> ListarAsync(FrequenciaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(FrequenciaCreateRequest request, CancellationToken ct); }
public interface IAvaliacaoService { Task<Result<PagedResult<AvaliacaoResponse>>> ListarAsync(TurmaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(AvaliacaoCreateRequest request, CancellationToken ct); Task<Result<long>> RegistrarNotaAsync(long avaliacaoId, NotaCreateRequest request, CancellationToken ct); }
public interface IBoletimService { Task<Result<BoletimResponse>> ObterAsync(long alunoId, CancellationToken ct); }
public interface IPreMatriculaService { Task<Result<PagedResult<PreMatriculaResponse>>> ListarAsync(PreMatriculaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(PreMatriculaCreateRequest request, CancellationToken ct); Task<Result> ConverterAsync(long id, ConverterPreMatriculaRequest request, CancellationToken ct); Task<Result> IndeferirAsync(long id, CancellationToken ct); }
public interface IEducacensoService { Task<Result<PagedResult<EducacensoRegistroResponse>>> ListarAsync(EscolaFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(EducacensoRegistroRequest request, CancellationToken ct); Task<Result> ValidarDevAsync(long id, CancellationToken ct); }
public interface IEducacaoDashboardService { Task<Result<EducacaoDashboardResponse>> ObterAsync(CancellationToken ct); }
public interface IEducacaoExportacaoService { Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct); }
