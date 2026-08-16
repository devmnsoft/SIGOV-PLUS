using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Frotas;

public sealed record FrotaRegistroDto(long Id,string? Codigo,string? Nome,string? Descricao,string Status,DateTimeOffset? DataReferencia,decimal? Quantidade,decimal? Valor);
public sealed record FrotaRegistroRequest(string? Codigo,string? Nome,string? Descricao,string Status="ATIVO",DateTimeOffset? DataReferencia=null,decimal? Quantidade=null,decimal? Valor=null,long? VeiculoId=null,long? MotoristaId=null,string? Documento=null,string? Placa=null);
public sealed record FrotasDashboardDto(long Veiculos,long Motoristas,long AbastecimentosMes,long ManutencoesAbertas,long ViagensAtivas);
public interface IFrotasRepository { Task<PagedResult<FrotaRegistroDto>> ListarAsync(long tenantId,string recurso,int pagina,int tamanho,CancellationToken ct); Task<long> CriarAsync(long tenantId,long? entidadeId,long? exercicioId,long? usuarioId,string recurso,FrotaRegistroRequest request,string correlationId,CancellationToken ct); Task<FrotasDashboardDto> DashboardAsync(long tenantId,CancellationToken ct); }
public interface IFrotasService { Task<Result<PagedResult<FrotaRegistroDto>>> ListarAsync(string recurso,int pagina,int tamanho,CancellationToken ct); Task<Result<long>> CriarAsync(string recurso,FrotaRegistroRequest request,string correlationId,CancellationToken ct); Task<Result<FrotasDashboardDto>> DashboardAsync(CancellationToken ct); }
