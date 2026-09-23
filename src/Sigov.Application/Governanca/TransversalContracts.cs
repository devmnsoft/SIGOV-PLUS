namespace Sigov.Application.Governanca;

public sealed record PendenciaOperacionalDto(long Id, string Modulo, string Recurso, string Tipo, string Entidade,
    string EntidadeId, string Gravidade, string Titulo, string? Descricao, DateTimeOffset? Prazo,
    long? ResponsavelUsuarioId, string RotaAcao, string Status, DateTimeOffset CreatedAt);

public sealed record AlertaOperacionalDto(long Id, string Modulo, string Tipo, string Severidade, string Titulo,
    string? Descricao, string RotaAcao, string Status, DateTimeOffset CreatedAt);

public sealed record QualidadeDadosDto(long Id, string Modulo, string Regra, string Entidade, string EntidadeId,
    string Severidade, string Descricao, string? RotaCorrecao, string Status, DateTimeOffset DetectedAt,
    long? ResponsavelUsuarioId = null, long Versao = 1, string? UltimoResultado = null, DateTimeOffset? VerificadoEm = null);

public sealed record GovernancaHistoricoDto(long Id, string Evento, long? UsuarioId, string? Justificativa,
    DateTimeOffset OcorridoEm);
public sealed record GovernancaOcorrenciaDto(long Id, string Tipo, string Modulo, string Titulo, string Descricao,
    string RegraOuMotivo, string Entidade, string EntidadeId, string Classificacao, string Status, string? RotaOrigem,
    long? ResponsavelUsuarioId, string? ResponsavelNome, DateTimeOffset? Prazo, DateTimeOffset DetectadaEm,
    DateTimeOffset? VerificadaEm, string? UltimoResultado, long Versao, IReadOnlyCollection<GovernancaHistoricoDto> Historico);
public sealed record GovernancaComandoResultado(bool Sucesso, string Codigo, string Mensagem, long Versao);

public sealed record IntegracaoInternaDto(string Origem, string Destino, string Status, DateTimeOffset? UltimoEvento,
    long QuantidadePendente, long QuantidadeErro, string? RotaCorrecao, bool Preparatoria);

public sealed record ModuloStatusFuncionalDto(string Modulo, bool Dashboard, bool Listagem, bool Cadastro,
    bool Edicao, bool Cancelamento, bool Exportacao, bool Permissoes, bool Auditoria, bool Lgpd,
    bool Integracao, bool Alertas, bool QualidadeDados, string Comprovacao, string StatusFinal);

public interface ITransversalGovernancaService
{
    Task<IReadOnlyCollection<PendenciaOperacionalDto>> ListarPendenciasAsync(string? modulo, string? gravidade, int pagina, int tamanho, CancellationToken ct);
    Task<IReadOnlyCollection<AlertaOperacionalDto>> ListarAlertasAsync(string? tipo, string? severidade, int pagina, int tamanho, CancellationToken ct);
    Task<IReadOnlyCollection<QualidadeDadosDto>> ListarQualidadeAsync(string? modulo, string? severidade, int pagina, int tamanho, CancellationToken ct);
    Task<GovernancaOcorrenciaDto?> ObterOcorrenciaAsync(string tipo, long id, CancellationToken ct);
    Task<GovernancaComandoResultado> AtribuirAsync(string tipo, long id, long responsavelUsuarioId, long versao, string justificativa, CancellationToken ct);
    Task<GovernancaComandoResultado> RevalidarQualidadeAsync(long id, long versao, CancellationToken ct);
    Task<IReadOnlyCollection<IntegracaoInternaDto>> ListarIntegracoesAsync(CancellationToken ct);
    Task<IReadOnlyCollection<ModuloStatusFuncionalDto>> ListarStatusFuncionalAsync(CancellationToken ct);
    Task<bool> ResolverAlertaAsync(long id, string justificativa, CancellationToken ct);
}
