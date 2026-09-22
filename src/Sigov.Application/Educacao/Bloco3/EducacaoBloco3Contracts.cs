using Sigov.Domain.Common;

namespace Sigov.Application.Educacao.Bloco3;

public sealed record EducacaoBloco3Filtro(long? AlunoId = null, long? EscolaId = null, string? Status = null, string? Tipo = null);
public sealed record EducacaoDocumentoEscolarDto(long Id, long AlunoId, long MatriculaId, string Tipo, string Status, string Titulo, DateTime CreatedAt);
public sealed record EducacaoEmitirDeclaracaoMatriculaRequest(long AlunoId, long MatriculaId, string Titulo);
public sealed record EducacaoEmitirDeclaracaoFrequenciaRequest(long AlunoId, long MatriculaId, string Titulo, DateOnly Inicio, DateOnly Fim);
public sealed record EducacaoFichaCadastralAlunoDto(long Id, string CodigoAluno, string Situacao, string DocumentoMascarado);
public sealed record EducacaoHistoricoEscolarDto(long AlunoId, IReadOnlyCollection<EducacaoHistoricoEscolarItemDto> Itens);
public sealed record EducacaoHistoricoEscolarItemDto(string ComponenteCurricular, decimal? Nota, decimal? Frequencia);
public sealed record EducacaoSolicitacaoEscolarDto(long Id, long AlunoId, string Tipo, string Status, string Descricao, DateTime CreatedAt);
public sealed record EducacaoCriarSolicitacaoEscolarRequest(long AlunoId, string Tipo, string Descricao, long? ResponsavelId = null);
public sealed record EducacaoDecidirSolicitacaoEscolarRequest(string Justificativa);
public sealed record EducacaoTransferenciaDto(long Id, long AlunoId, long MatriculaId, string Status, string Descricao, DateTime CreatedAt);
public sealed record EducacaoSolicitarTransferenciaRequest(long AlunoId, long MatriculaId, long? EscolaDestinoId, long? TurmaDestinoId, string? JustificativaExterna);
public sealed record EducacaoDecidirTransferenciaRequest(string Justificativa);
public sealed record EducacaoOcorrenciaEscolarDto(long Id, long AlunoId, string Tipo, string Descricao, bool VisivelPortal, bool Sensivel, DateTime DataOcorrencia);
public sealed record EducacaoCriarOcorrenciaEscolarRequest(long AlunoId, long? MatriculaId, string Tipo, string Descricao, DateTime DataOcorrencia, bool VisivelPortal, bool Sensivel);
public sealed record EducacaoPendenciaDocumentalDto(long Id, long AlunoId, string Tipo, string Status, DateTime? DataVencimento, bool Vencida);
public sealed record EducacaoCriarPendenciaDocumentalRequest(long AlunoId, long? MatriculaId, string Tipo, string Descricao, DateTime? DataVencimento);

public sealed record EducacaoDiarioClasseDto(long Id, long EscolaId, long TurmaId, long DisciplinaId, long ProfessorId, string Periodo, string Status);
public sealed record EducacaoCriarDiarioClasseRequest(long EscolaId, long TurmaId, long DisciplinaId, long ProfessorId, long AnoLetivoId, string Periodo);
public sealed record EducacaoDiarioAulaDto(long Id, long DiarioId, DateOnly DataAula, decimal CargaHoraria, string Status);
public sealed record EducacaoCriarDiarioAulaRequest(DateOnly DataAula, decimal CargaHoraria, string? Observacoes);
public sealed record EducacaoDiarioConteudoRequest(long AulaId, string Conteudo, string? Observacoes);
public sealed record EducacaoDiarioFrequenciaItemRequest(long AlunoId, string Status, string? Justificativa);
public sealed record EducacaoDiarioFrequenciaRequest(long AulaId, IReadOnlyCollection<EducacaoDiarioFrequenciaItemRequest> Alunos);
public sealed record EducacaoDiarioAvaliacaoRequest(long? AulaId, string Titulo, decimal ValorMaximo, decimal Peso);
public sealed record EducacaoDiarioReposicaoRequest(long AulaId, DateOnly DataReposicao, string Justificativa);
public sealed record EducacaoDiarioFechamentoRequest(string Observacao, string TokenConferencia);
public sealed record EducacaoDiarioConferenciaDto(long DiarioId, string Status, long AlunosAbrangidos, long Aulas, long LancamentosEsperados, long LancamentosRealizados, long Pendencias, IReadOnlyCollection<EducacaoDiarioPendenciaDetalheDto> Detalhes, string TokenConferencia, DateTimeOffset UltimaAtualizacao, bool PodeFechar);
public sealed record EducacaoDiarioPendenciaDetalheDto(string Tipo, string Registro, string Problema, string Acao, string? UrlCorrecao);
public sealed record EducacaoDiarioFechamentoDto(long Id, long DiarioId, int Versao, string Status, string Justificativa, DateTimeOffset CreatedAt, long CreatedBy, long? RetificaFechamentoId);
public sealed record EducacaoDiarioReaberturaRequest(string Justificativa);
public sealed record EducacaoDiarioPendenciaDto(long Id, long DiarioId, string Tipo, string Descricao, string Status);

public sealed record EducacaoPortalResumoDto(IReadOnlyCollection<EducacaoPortalAlunoDto> Alunos, int ComunicadosNaoLidos, int SolicitacoesAbertas);
public sealed record EducacaoPortalAlunoDto(long Id, string Nome, string CodigoAluno, string Situacao, string? Escola, string? Turma, int? AnoLetivo);
public sealed record EducacaoPortalVidaEscolarDto(long AlunoId, long MatriculaId, string Situacao, string Escola, string Turma, int AnoLetivo, int AulasLancadas, int Presencas, int Faltas, decimal? PercentualFrequencia, DateTimeOffset AtualizadoEm);
public sealed record EducacaoPortalBoletimDto(long AlunoId, string Situacao, int Versao, DateTimeOffset AtualizadoEm, IReadOnlyCollection<EducacaoHistoricoEscolarItemDto> Itens);
public sealed record EducacaoPortalOcorrenciaDto(long Id, long AlunoId, string Tipo, string Descricao, DateTime DataOcorrencia);
public sealed record EducacaoPortalSolicitacaoDto(long Id, long AlunoId, string Tipo, string Status, string Descricao, DateTime CreatedAt);
public sealed record EducacaoPortalCriarSolicitacaoRequest(long AlunoId, string Tipo, string Descricao);
public sealed record EducacaoPortalComunicadoDto(long Id, long AlunoId, string Titulo, string Mensagem, string Status, int Versao, bool ExigeCiencia, DateTime? LidoEm, DateTime? CienciaEm, DateTime CreatedAt);
public sealed record EducacaoPortalMensagemDto(long Id, string Titulo, string Mensagem, bool Lida, DateTime CreatedAt);
public sealed record EducacaoPortalVinculoDto(long Id, long UsuarioId, long AlunoId, long? ResponsavelId, string Status);
public sealed record EducacaoPortalCriarVinculoRequest(long UsuarioVinculadoId, long AlunoId, long? ResponsavelId);
public sealed record EducacaoPortalResponderSolicitacaoRequest(string Resposta, string Status);
public sealed record EducacaoPortalCriarComunicadoRequest(string Titulo, string Mensagem, long? EscolaId, long? TurmaId, bool ExigeCiencia = false);
public sealed record EducacaoPortalAlterarVinculoRequest(string Justificativa);
public sealed record EducacaoPortalCienciaDto(long ComunicadoId, long AlunoId, int Versao, DateTimeOffset ConfirmadaEm);

public interface IEducacaoBloco3Repository
{
    Task<IReadOnlyCollection<T>> ListarAsync<T>(long tenantId, string recurso, EducacaoBloco3Filtro filtro, long? usuarioId, bool administrativo, CancellationToken ct);
    Task<T?> ObterAsync<T>(long tenantId, string recurso, long id, long? usuarioId, bool administrativo, CancellationToken ct);
    Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, string recurso, object dados, long usuarioId, string correlationId, CancellationToken ct);
    Task AlterarStatusAsync(long tenantId, string recurso, long id, string status, string justificativa, long usuarioId, string correlationId, CancellationToken ct);
    Task<bool> MatriculaValidaAsync(long tenantId, long alunoId, long matriculaId, CancellationToken ct);
    Task<bool> UsuarioVinculadoAsync(long tenantId, long usuarioId, long alunoId, CancellationToken ct);
    Task<string?> ObterStatusAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<bool> DiarioProntoParaFechamentoAsync(long tenantId, long diarioId, CancellationToken ct);
    Task<EducacaoDiarioConferenciaDto?> ConferirDiarioAsync(long tenantId, long diarioId, CancellationToken ct);
    Task<long> FecharDiarioAsync(long tenantId, long diarioId, string tokenConferencia, string justificativa, long usuarioId, string correlationId, CancellationToken ct);
    Task ReabrirDiarioAsync(long tenantId, long diarioId, string justificativa, long usuarioId, string correlationId, CancellationToken ct);
    Task<IReadOnlyCollection<EducacaoDiarioFechamentoDto>> HistoricoFechamentoAsync(long tenantId, long diarioId, CancellationToken ct);
    Task<IReadOnlyCollection<EducacaoPortalAlunoDto>> ListarAlunosAutorizadosAsync(long tenantId, long usuarioId, CancellationToken ct);
    Task<EducacaoPortalVidaEscolarDto?> ObterVidaEscolarAsync(long tenantId, long usuarioId, long alunoId, CancellationToken ct);
    Task<EducacaoPortalBoletimDto?> ObterBoletimPortalAsync(long tenantId, long usuarioId, long alunoId, CancellationToken ct);
    Task<EducacaoPortalCienciaDto> RegistrarCienciaAsync(long tenantId, long usuarioId, long comunicadoId, long alunoId, CancellationToken ct);
    Task RegistrarLeituraAsync(long tenantId, long usuarioId, long comunicadoId, long alunoId, CancellationToken ct);
    Task AlterarVinculoAsync(long tenantId, long vinculoId, string status, string justificativa, long usuarioId, string correlationId, CancellationToken ct);
}

public interface IEducacaoSecretariaRepository : IEducacaoBloco3Repository { }
public interface IEducacaoDiarioClasseRepository : IEducacaoBloco3Repository { }
public interface IEducacaoPortalRepository : IEducacaoBloco3Repository { }

public interface IEducacaoSecretariaService
{
    Task<Result<IReadOnlyCollection<T>>> ListarAsync<T>(string recurso, EducacaoBloco3Filtro filtro, CancellationToken ct);
    Task<Result<long>> CriarAsync(string recurso, object request, CancellationToken ct);
    Task<Result> DecidirAsync(string recurso, long id, string status, string justificativa, CancellationToken ct);
    Task<Result<EducacaoDiarioConferenciaDto>> ConferirDiarioAsync(long diarioId, CancellationToken ct);
    Task<Result<long>> FecharDiarioAsync(long diarioId, EducacaoDiarioFechamentoRequest request, CancellationToken ct);
    Task<Result> ReabrirDiarioAsync(long diarioId, EducacaoDiarioReaberturaRequest request, CancellationToken ct);
    Task<Result<IReadOnlyCollection<EducacaoDiarioFechamentoDto>>> HistoricoFechamentoAsync(long diarioId, CancellationToken ct);
}
public interface IEducacaoDocumentoEscolarService : IEducacaoSecretariaService { }
public interface IEducacaoTransferenciaService : IEducacaoSecretariaService { }
public interface IEducacaoSolicitacaoEscolarService : IEducacaoSecretariaService { }
public interface IEducacaoDiarioClasseService : IEducacaoSecretariaService { }
public interface IEducacaoDiarioFrequenciaService : IEducacaoSecretariaService { }
public interface IEducacaoDiarioFechamentoService : IEducacaoSecretariaService { }
public interface IEducacaoPortalService : IEducacaoSecretariaService
{
    Task<Result<IReadOnlyCollection<EducacaoPortalAlunoDto>>> ListarAlunosAsync(CancellationToken ct);
    Task<Result<EducacaoPortalVidaEscolarDto>> ObterVidaEscolarAsync(long alunoId, CancellationToken ct);
    Task<Result<EducacaoPortalBoletimDto>> ObterBoletimAsync(long alunoId, CancellationToken ct);
    Task<Result<EducacaoPortalCienciaDto>> ConfirmarCienciaAsync(long comunicadoId, long alunoId, CancellationToken ct);
    Task<Result> RegistrarLeituraAsync(long comunicadoId, long alunoId, CancellationToken ct);
    Task<Result> AlterarVinculoAsync(long vinculoId, bool ativar, string justificativa, CancellationToken ct);
}
public interface IEducacaoPortalSolicitacaoService : IEducacaoSecretariaService { }
public interface IEducacaoComunicadoService : IEducacaoSecretariaService { }
