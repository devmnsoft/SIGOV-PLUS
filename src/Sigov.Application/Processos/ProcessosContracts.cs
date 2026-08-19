using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Processos;

public static class ProcessosPermissoes
{
    public const string Modulo = "processos";
    public const string TipoVisualizar = "processos.tipo.visualizar";
    public const string TipoCriar = "processos.tipo.criar";
    public const string TipoEditar = "processos.tipo.editar";
    public const string TipoExcluir = "processos.tipo.excluir";
    public const string ProcessoVisualizar = "processos.processo.visualizar";
    public const string ProcessoCriar = "processos.processo.criar";
    public const string ProcessoEditar = "processos.processo.editar";
    public const string ProcessoExcluir = "processos.processo.excluir";
    public const string ProcessoMovimentar = "processos.processo.movimentar";
    public const string ProcessoParecer = "processos.processo.parecer";
    public const string ProcessoEncerrar = "processos.processo.encerrar";
    public const string ProcessoCancelar = "processos.processo.cancelar";
    public const string ProtocoloVisualizar = "processos.protocolo.visualizar";
    public const string ProtocoloCriar = "processos.protocolo.criar";
    public const string ProtocoloEditar = "processos.protocolo.editar";
    public const string ProtocoloEncerrar = "processos.protocolo.encerrar";
    public const string ProtocoloConverter = "processos.protocolo.converter";
    public const string OuvidoriaVisualizar = "processos.ouvidoria.visualizar";
    public const string OuvidoriaCriar = "processos.ouvidoria.criar";
    public const string OuvidoriaResponder = "processos.ouvidoria.responder";
    public const string OuvidoriaConverter = "processos.ouvidoria.converter";
    public const string OuvidoriaArquivar = "processos.ouvidoria.arquivar";
    public const string DiarioVisualizar = "processos.diario.visualizar";
    public const string DiarioCriar = "processos.diario.criar";
    public const string DiarioEditar = "processos.diario.editar";
    public const string DiarioPublicar = "processos.diario.publicar";
}

public sealed record CriarTipoProcessoRequest(string Nome, string? Descricao, int? PrazoPadraoDias, bool ExigeInteressado, bool PermiteSigilo);
public sealed record AtualizarTipoProcessoRequest(string Nome, string? Descricao, int? PrazoPadraoDias, bool ExigeInteressado, bool PermiteSigilo, bool Ativo);
public sealed record TipoProcessoResponse(long Id, string Nome, string? Descricao, int? PrazoPadraoDias, bool ExigeInteressado, bool PermiteSigilo, bool Ativo);

public sealed record CriarProcessoRequest(long TipoProcessoId, string Assunto, string? Descricao, long? InteressadoPessoaId, long? UnidadeOrigemId, string Prioridade, bool Sigiloso, DateTimeOffset? PrazoRespostaAt, string? Observacao);
public sealed record AtualizarProcessoRequest(long TipoProcessoId, string Assunto, string? Descricao, string Prioridade, bool Sigiloso, DateTimeOffset? PrazoRespostaAt);
public sealed record MovimentarProcessoRequest(long? UnidadeDestinoId, long? UsuarioDestinoId, string Despacho, string? StatusNovo);
public sealed record EmitirParecerRequest(string Titulo, string Texto, string TipoParecer, bool Sigiloso);
public sealed record CancelarProcessoRequest(string Justificativa);
public sealed record ProcessoFiltro(int Page = 1, int PageSize = 20, string? Numero = null, string? Assunto = null, long? TipoProcessoId = null, string? Status = null, string? Prioridade = null, long? InteressadoPessoaId = null, long? UnidadeAtualId = null, DateTimeOffset? Inicio = null, DateTimeOffset? Fim = null, bool? Sigiloso = null);
public sealed record ProcessoResumoResponse(long Id, string Numero, string Assunto, string TipoProcesso, string? Interessado, string Status, string Prioridade, DateTimeOffset DataAbertura, DateTimeOffset? PrazoRespostaAt, bool Sigiloso);
public sealed record ProcessoDetalheResponse(long Id, string Numero, string Assunto, string? Descricao, string TipoProcesso, string? Interessado, string Status, string Prioridade, DateTimeOffset DataAbertura, DateTimeOffset? PrazoRespostaAt, bool Sigiloso, IReadOnlyCollection<ProcessoMovimentacaoResponse> Movimentacoes, IReadOnlyCollection<ProcessoParecerResponse> Pareceres);
public sealed record ProcessoMovimentacaoResponse(long Id, string Despacho, string? StatusAnterior, string? StatusNovo, DateTimeOffset MovimentadoAt);
public sealed record ProcessoParecerResponse(long Id, string Titulo, string Texto, string TipoParecer, bool Sigiloso, DateTimeOffset ParecerAt);

public sealed record CriarProtocoloRequest(long? PessoaId, string Assunto, string? Descricao, string Canal, long? UsuarioResponsavelId);
public sealed record ProtocoloFiltro(int Page = 1, int PageSize = 20, string? Numero = null, string? Status = null, long? PessoaId = null);
public sealed record ProtocoloResumoResponse(long Id, string Numero, string Assunto, string Canal, string Status, string? Pessoa, DateTimeOffset AbertoAt);
public sealed record ProtocoloDetalheResponse(long Id, string Numero, string Assunto, string? Descricao, string Canal, string Status, string? Pessoa, long? ProcessoDigitalId, DateTimeOffset AbertoAt);
public sealed record ConverterProtocoloEmProcessoRequest(long TipoProcessoId, string Prioridade, long? UnidadeOrigemId);

public sealed record CriarOuvidoriaRequest(long? PessoaId, string TipoManifestacao, string Assunto, string Descricao, bool Anonima, bool Sigilosa);
public sealed record ResponderOuvidoriaRequest(string Resposta);
public sealed record OuvidoriaFiltro(int Page = 1, int PageSize = 20, string? Numero = null, string? Status = null, string? TipoManifestacao = null);
public sealed record OuvidoriaResumoResponse(long Id, string Numero, string TipoManifestacao, string Assunto, string Status, bool Anonima, bool Sigilosa, DateTimeOffset CreatedAt);
public sealed record OuvidoriaDetalheResponse(long Id, string Numero, string TipoManifestacao, string Assunto, string Descricao, string Status, bool Anonima, bool Sigilosa, string? Pessoa, string? Resposta, long? ProcessoDigitalId);

public sealed record CriarDiarioPublicacaoRequest(string NumeroEdicao, DateOnly DataPublicacao, string Titulo, string? Descricao);
public sealed record PublicarDiarioRequest(DateTimeOffset? PublicadoAt);
public sealed record CriarAtoOficialRequest(string TipoAto, string? Numero, string Titulo, string Texto, DateOnly? DataAto, string? Origem);
public sealed record DiarioFiltro(int Page = 1, int PageSize = 20, string? Status = null, DateOnly? Inicio = null, DateOnly? Fim = null);
public sealed record DiarioPublicacaoResponse(long Id, string NumeroEdicao, DateOnly DataPublicacao, string Titulo, string? Descricao, string Status, DateTimeOffset? PublicadoAt, IReadOnlyCollection<AtoOficialResponse> Atos);
public sealed record AtoOficialResponse(long Id, long DiarioOficialPublicacaoId, string TipoAto, string? Numero, string Titulo, string Texto, DateOnly? DataAto, string? Origem);

public interface ITipoProcessoRepository { Task<PagedResult<TipoProcessoResponse>> ListarAsync(long tenantId, long? entidadeId, int page, int pageSize, CancellationToken cancellationToken); Task<TipoProcessoResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken); Task<long> CriarAsync(long tenantId, long? entidadeId, CriarTipoProcessoRequest request, long? usuarioId, Guid correlationId, CancellationToken cancellationToken); Task AtualizarAsync(long tenantId, long id, AtualizarTipoProcessoRequest request, long? usuarioId, CancellationToken cancellationToken); Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken); }
public interface IProcessoDigitalRepository { Task<PagedResult<ProcessoResumoResponse>> ListarAsync(long tenantId, long? entidadeId, long? exercicioId, ProcessoFiltro filtro, CancellationToken cancellationToken); Task<ProcessoDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken); Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, int ano, CriarProcessoRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken); Task AtualizarAsync(long tenantId, long id, AtualizarProcessoRequest request, long? usuarioId, CancellationToken cancellationToken); Task AlterarStatusAsync(long tenantId, long id, string status, long? usuarioId, CancellationToken cancellationToken); Task ExcluirAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken); }
public interface IProcessoMovimentacaoRepository { Task<long> CriarAsync(long tenantId, long processoId, MovimentarProcessoRequest request, long usuarioId, CancellationToken cancellationToken); }
public interface IProcessoParecerRepository { Task<long> CriarAsync(long tenantId, long processoId, EmitirParecerRequest request, long usuarioId, CancellationToken cancellationToken); }
public interface IProtocoloAtendimentoRepository { Task<PagedResult<ProtocoloResumoResponse>> ListarAsync(long tenantId, ProtocoloFiltro filtro, CancellationToken cancellationToken); Task<ProtocoloDetalheResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken); Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, CriarProtocoloRequest request, long? usuarioId, CancellationToken cancellationToken); Task AtualizarAsync(long tenantId, long id, CriarProtocoloRequest request, long? usuarioId, CancellationToken cancellationToken); Task VincularProcessoAsync(long tenantId, long id, long processoId, long? usuarioId, CancellationToken cancellationToken); Task EncerrarAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken); }
public interface IOuvidoriaRepository { Task<PagedResult<OuvidoriaResumoResponse>> ListarAsync(long tenantId, OuvidoriaFiltro filtro, CancellationToken cancellationToken); Task<OuvidoriaDetalheResponse?> ObterAsync(long tenantId, long id, bool mascarar, CancellationToken cancellationToken); Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, string numero, CriarOuvidoriaRequest request, long? usuarioId, CancellationToken cancellationToken); Task ResponderAsync(long tenantId, long id, ResponderOuvidoriaRequest request, long usuarioId, CancellationToken cancellationToken); Task VincularProcessoAsync(long tenantId, long id, long processoId, long? usuarioId, CancellationToken cancellationToken); Task ArquivarAsync(long tenantId, long id, long? usuarioId, CancellationToken cancellationToken); }
public interface IDiarioOficialRepository { Task<PagedResult<DiarioPublicacaoResponse>> ListarAsync(long tenantId, DiarioFiltro filtro, CancellationToken cancellationToken); Task<DiarioPublicacaoResponse?> ObterAsync(long tenantId, long id, CancellationToken cancellationToken); Task<long> CriarAsync(long tenantId, long? entidadeId, long? exercicioId, CriarDiarioPublicacaoRequest request, long? usuarioId, CancellationToken cancellationToken); Task AtualizarAsync(long tenantId, long id, CriarDiarioPublicacaoRequest request, long? usuarioId, CancellationToken cancellationToken); Task PublicarAsync(long tenantId, long id, long usuarioId, CancellationToken cancellationToken); Task<long> CriarAtoAsync(long tenantId, long publicacaoId, CriarAtoOficialRequest request, long? usuarioId, CancellationToken cancellationToken); Task<IReadOnlyCollection<AtoOficialResponse>> ListarAtosAsync(long tenantId, long publicacaoId, CancellationToken cancellationToken); }
public interface IProcessoSequencialService { Task<string> ProximoAsync(long tenantId, long? entidadeId, long? exercicioId, int ano, string chave, string prefixo, CancellationToken cancellationToken); }

public interface ITipoProcessoService { Task<Result<PagedResult<TipoProcessoResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken); Task<Result<TipoProcessoResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(CriarTipoProcessoRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, AtualizarTipoProcessoRequest request, CancellationToken cancellationToken); Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken); }
public interface IProcessoDigitalService { Task<Result<PagedResult<ProcessoResumoResponse>>> ListarAsync(ProcessoFiltro filtro, CancellationToken cancellationToken); Task<Result<ProcessoDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(CriarProcessoRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, AtualizarProcessoRequest request, CancellationToken cancellationToken); Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken); Task<Result<long>> MovimentarAsync(long id, MovimentarProcessoRequest request, CancellationToken cancellationToken); Task<Result<long>> EmitirParecerAsync(long id, EmitirParecerRequest request, CancellationToken cancellationToken); Task<Result> EncerrarAsync(long id, CancellationToken cancellationToken); Task<Result> CancelarAsync(long id, CancelarProcessoRequest request, CancellationToken cancellationToken); }
public interface IProtocoloAtendimentoService { Task<Result<PagedResult<ProtocoloResumoResponse>>> ListarAsync(ProtocoloFiltro filtro, CancellationToken cancellationToken); Task<Result<ProtocoloDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(CriarProtocoloRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, CriarProtocoloRequest request, CancellationToken cancellationToken); Task<Result<long>> ConverterEmProcessoAsync(long id, ConverterProtocoloEmProcessoRequest request, CancellationToken cancellationToken); Task<Result> EncerrarAsync(long id, CancellationToken cancellationToken); }
public interface IOuvidoriaService { Task<Result<PagedResult<OuvidoriaResumoResponse>>> ListarAsync(OuvidoriaFiltro filtro, CancellationToken cancellationToken); Task<Result<OuvidoriaDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(CriarOuvidoriaRequest request, CancellationToken cancellationToken); Task<Result> ResponderAsync(long id, ResponderOuvidoriaRequest request, CancellationToken cancellationToken); Task<Result<long>> ConverterEmProcessoAsync(long id, ConverterProtocoloEmProcessoRequest request, CancellationToken cancellationToken); Task<Result> ArquivarAsync(long id, CancellationToken cancellationToken); }
public interface IDiarioOficialService { Task<Result<PagedResult<DiarioPublicacaoResponse>>> ListarAsync(DiarioFiltro filtro, CancellationToken cancellationToken); Task<Result<DiarioPublicacaoResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(CriarDiarioPublicacaoRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, CriarDiarioPublicacaoRequest request, CancellationToken cancellationToken); Task<Result> PublicarAsync(long id, PublicarDiarioRequest request, CancellationToken cancellationToken); Task<Result<long>> CriarAtoAsync(long id, CriarAtoOficialRequest request, CancellationToken cancellationToken); Task<Result<IReadOnlyCollection<AtoOficialResponse>>> ListarAtosAsync(long id, CancellationToken cancellationToken); }
