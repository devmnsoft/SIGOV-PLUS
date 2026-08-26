namespace Sigov.Domain.Transversal;

public sealed record EvidenciaTransversal(
    long Id,
    long TenantId,
    long EntidadeId,
    string Tipo,
    string Origem,
    string EntidadeRelacionadaTipo,
    long EntidadeRelacionadaId,
    string Descricao,
    DateTimeOffset RegistradaAt,
    decimal? Latitude,
    decimal? Longitude,
    string? HashSha256,
    long? GedDocumentoId,
    long UsuarioResponsavelId,
    string ClassificacaoLgpd);

public sealed record ItemSelecaoRelacionada(long Id, string Codigo, string Descricao);

public enum StatusSincronizacao
{
    Pendente,
    Processando,
    Concluido,
    Falha
}

public sealed record EventoSincronizacao(
    long Id,
    long TenantId,
    long EntidadeId,
    string ChaveIdempotente,
    string Origem,
    string Payload,
    StatusSincronizacao Status,
    int Tentativas,
    string? ErroSanitizado,
    DateTimeOffset CriadoAt,
    DateTimeOffset? ProcessamentoAt,
    DateTimeOffset? ConcluidoAt);
