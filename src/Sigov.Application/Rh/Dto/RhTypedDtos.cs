namespace Sigov.Application.Rh.Dto;

public sealed record ServidorCreateRequest(string Matricula, string Nome, string Cpf, DateOnly? DataNascimento = null, string? Email = null, string? EmailInstitucional = null, string? Telefone = null, long? PessoaId = null, string? Banco = null, string? Agencia = null, string? Conta = null);
public sealed record ServidorUpdateRequest(string Matricula, string Nome, string Cpf, DateOnly? DataNascimento = null, string? Email = null, string? EmailInstitucional = null, string? Telefone = null, long? PessoaId = null, bool Ativo = true, string? Banco = null, string? Agencia = null, string? Conta = null);
public sealed record ServidorResponse(long Id, string Matricula, string Nome, string Cpf, DateOnly? DataNascimento, string? Email, string? EmailInstitucional, string? Telefone, long? PessoaId, bool Ativo, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt, string ClassificacaoLgpd);
public sealed record ServidorFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, string? Matricula = null, string? Nome = null, string? Cpf = null);

public sealed record CargoCreateRequest(string Codigo, string Nome, string? Cbo = null, decimal? VencimentoBase = null);
public sealed record CargoUpdateRequest(string Codigo, string Nome, string? Cbo = null, decimal? VencimentoBase = null, bool Ativo = true);
public sealed record CargoResponse(long Id, string Codigo, string Nome, string? Cbo, decimal? VencimentoBase, bool Ativo);
public sealed record CargoFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, string? Codigo = null);

public sealed record LotacaoCreateRequest(string Codigo, string Nome, long? LotacaoPaiId = null);
public sealed record LotacaoUpdateRequest(string Codigo, string Nome, long? LotacaoPaiId = null, bool Ativo = true);
public sealed record LotacaoResponse(long Id, string Codigo, string Nome, long? LotacaoPaiId, bool Ativo);
public sealed record LotacaoFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, string? Codigo = null);

public sealed record VinculoCreateRequest(long ServidorId, long CargoId, long LotacaoId, string Tipo, DateOnly DataAdmissao, DateOnly? DataDesligamento = null);
public sealed record VinculoUpdateRequest(long ServidorId, long CargoId, long LotacaoId, string Tipo, DateOnly DataAdmissao, DateOnly? DataDesligamento = null, bool Ativo = true);
public sealed record VinculoResponse(long Id, long ServidorId, long CargoId, long LotacaoId, string Tipo, DateOnly? DataAdmissao, DateOnly? DataDesligamento, bool Ativo);
public sealed record VinculoFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, long? ServidorId = null);

public sealed record FolhaCreateRequest(int Ano, int Mes, string Tipo, string Status = "Aberta");
public sealed record FolhaUpdateRequest(int Ano, int Mes, string Tipo, string Status = "Aberta", bool Ativo = true);
public sealed record FolhaResponse(long Id, int Ano, int Mes, string Tipo, string Status, bool Ativo);
public sealed record FolhaFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, int? Ano = null, int? Mes = null, string? Status = null);

public sealed record FolhaEventoCreateRequest(string Codigo, string Descricao, string Tipo, bool IncideInss = false, bool IncideIrrf = false);
public sealed record FolhaEventoUpdateRequest(string Codigo, string Descricao, string Tipo, bool IncideInss = false, bool IncideIrrf = false, bool Ativo = true);
public sealed record FolhaEventoResponse(long Id, string Codigo, string Descricao, string Tipo, bool IncideInss, bool IncideIrrf, bool Ativo);

public sealed record FolhaLancamentoCreateRequest(long FolhaId, long ServidorId, long EventoId, decimal Valor, string? Historico = null);
public sealed record FolhaLancamentoResponse(long Id, long FolhaId, long ServidorId, long EventoId, decimal Valor, string? Historico, bool Ativo);

public sealed record PontoCreateRequest(long ServidorId, DateTimeOffset DataHora, string Tipo, string? Origem = null);
public sealed record PontoResponse(long Id, long ServidorId, DateTimeOffset? DataHora, string Tipo, string? Origem, bool Ativo);
public sealed record PontoFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null, long? ServidorId = null);

public sealed record FeriasCreateRequest(long ServidorId, DateOnly Inicio, DateOnly Fim, string Status = "Programada");
public sealed record FeriasUpdateRequest(long ServidorId, DateOnly Inicio, DateOnly Fim, string Status = "Programada", bool Ativo = true);
public sealed record FeriasResponse(long Id, long ServidorId, DateOnly? Inicio, DateOnly? Fim, string Status, bool Ativo);

public sealed record AfastamentoCreateRequest(long ServidorId, DateOnly Inicio, DateOnly? Fim, string Motivo, string Status = "Solicitado");
public sealed record AfastamentoUpdateRequest(long ServidorId, DateOnly Inicio, DateOnly? Fim, string Motivo, string Status = "Solicitado", bool Ativo = true);
public sealed record AfastamentoResponse(long Id, long ServidorId, DateOnly? Inicio, DateOnly? Fim, string Motivo, string Status, bool Ativo);

public sealed record SaudeOcupacionalCreateRequest(long ServidorId, string Tipo, DateOnly DataAtendimento, string Status, string? ResultadoExame = null, string? Observacao = null);
public sealed record SaudeOcupacionalResponse(long Id, long ServidorId, string Tipo, DateOnly? DataAtendimento, string Status, string? ResultadoExame, string? Observacao, bool Ativo);

public sealed record EsocialEventoCreateRequest(string Evento, long ServidorId, string Status = "Pendente", string? Protocolo = null);
public sealed record EsocialEventoResponse(long Id, string Evento, long ServidorId, string Status, string? Protocolo, bool Ativo);

public sealed record ContrachequeResumoResponse(long Id, decimal Valor, string? Competencia, string? Historico);
public sealed record PortalServidorResponse(long ServidorId, string Nome, IReadOnlyCollection<ContrachequeResumoResponse> Contracheques, IReadOnlyCollection<FeriasResponse> Ferias, IReadOnlyCollection<AfastamentoResponse> Afastamentos);
