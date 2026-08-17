using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Bloco8;

public sealed record Bloco8RegistroDto(long Id,string? Codigo,string? Numero,int? Ano,string? Tipo,string Status,string? Titulo,string? Descricao,DateTimeOffset CreatedAt,bool Sigiloso);
public sealed record Bloco8RegistroRequest(string? Codigo,string? Tipo,string? Titulo,string? Descricao,string Status="ATIVO",long? ReferenciaId=null,long? ResponsavelId=null,DateTimeOffset? PrazoAt=null,string? Justificativa=null,bool Sigiloso=false,Dictionary<string,object?>? Dados=null);
public sealed record Bloco8DashboardDto(long Total,long Pendentes,long Vencidos,long Concluidos,IReadOnlyCollection<Bloco8RegistroDto> Recentes);
public sealed record ProcessoDigitalDto(long Id,string? Codigo,string? Numero,int? Ano,string? Tipo,string Status,string? Titulo,string? Descricao,DateTimeOffset CreatedAt,bool Sigiloso);
public sealed record ProcessoDigitalCriarRequest(string Tipo,string Assunto,string SetorOrigem,string Interessado,string? Descricao=null,bool Sigiloso=false);
public sealed record ProcessoDigitalAtualizarRequest(string Assunto,string? Descricao,string Status="EM_TRAMITACAO");
public sealed record ProcessoMovimentarRequest(string Origem,string Destino,long ResponsavelId,string Despacho);
public sealed record ProcessoEncerrarRequest(string Justificativa); public sealed record ProcessoReabrirRequest(string Justificativa);
public sealed record GedDocumentoCriarRequest(string Titulo,string Tipo,string Classificacao,string HashSha256,bool Sensivel=false,long? ReferenciaId=null);
public sealed record AssinaturaCriarRequest(long DocumentoId,string Fluxo="SEQUENCIAL",DateTimeOffset? PrazoAt=null);
public sealed record LegislativoCriarProposicaoRequest(string Tipo,string Ementa,string Autor,int Ano);
public sealed record LegislativoCriarSessaoRequest(string Tipo,DateTimeOffset Data,string Titulo);
public sealed record DiarioOficialCriarEdicaoRequest(long Numero,DateTimeOffset Data,string Responsavel);
public sealed record EsicCriarPedidoRequest(string Solicitante,string Assunto,string Descricao,string Canal);
public sealed record OuvidoriaCriarManifestacaoRequest(string Tipo,string Descricao,bool Sigilosa=false);
public sealed record AtendimentoDigitalCriarChamadoRequest(string Tipo,string Titulo,string Descricao,string Canal);

public interface IBloco8Repository { Task<PagedResult<Bloco8RegistroDto>> ListarAsync(long tenantId,string tabela,int pagina,int tamanho,CancellationToken ct); Task<Bloco8RegistroDto?> ObterAsync(long tenantId,string tabela,long id,CancellationToken ct); Task<long> CriarAsync(long tenantId,long? entidadeId,long? exercicioId,long? usuarioId,string tabela,Bloco8RegistroRequest request,string correlationId,CancellationToken ct); Task<bool> AlterarStatusAsync(long tenantId,string tabela,long id,string status,string? justificativa,long? usuarioId,string correlationId,CancellationToken ct); Task<Bloco8DashboardDto> DashboardAsync(long tenantId,string tabela,CancellationToken ct); }
public interface IBloco8Service { Task<Result<PagedResult<Bloco8RegistroDto>>> ListarAsync(string recurso,int pagina,int tamanho,CancellationToken ct); Task<Result<Bloco8RegistroDto>> ObterAsync(string recurso,long id,CancellationToken ct); Task<Result<long>> CriarAsync(string recurso,Bloco8RegistroRequest request,string correlationId,CancellationToken ct); Task<Result> AlterarStatusAsync(string recurso,long id,string status,string? justificativa,string correlationId,CancellationToken ct); Task<Result<Bloco8DashboardDto>> DashboardAsync(string recurso,CancellationToken ct); }
public interface IProcessosDigitaisRepository:IBloco8Repository{} public interface IGedRepository:IBloco8Repository{} public interface IAssinaturaRepository:IBloco8Repository{} public interface ILegislativoRepository:IBloco8Repository{} public interface ITransparenciaRepository:IBloco8Repository{} public interface IDiarioOficialRepository:IBloco8Repository{}
public interface IProcessosDigitaisService:IBloco8Service{} public interface IProtocoloDigitalService:IBloco8Service{} public interface IProcessoNumeroService:IBloco8Service{} public interface IProcessoMovimentacaoService:IBloco8Service{} public interface IProcessoRelatorioService:IBloco8Service{}
public interface IGedService:IBloco8Service{} public interface IGedDocumentoService:IBloco8Service{} public interface IGedArquivoFisicoService:IBloco8Service{} public interface IGedValidacaoPublicaService:IBloco8Service{} public interface IAssinaturaService:IBloco8Service{}
public interface ILegislativoService:IBloco8Service{} public interface ILegislativoProposicaoService:IBloco8Service{} public interface ILegislativoSessaoService:IBloco8Service{} public interface ILegislativoVotacaoService:IBloco8Service{} public interface ILegislativoNormaService:IBloco8Service{} public interface ILegislativoRelatorioService:IBloco8Service{}
public interface ITransparenciaService:IBloco8Service{} public interface IDiarioOficialService:IBloco8Service{} public interface IEsicService:IBloco8Service{} public interface IOuvidoriaService:IBloco8Service{} public interface IAtendimentoDigitalService:IBloco8Service{}
