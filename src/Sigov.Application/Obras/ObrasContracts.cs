using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Obras;

public sealed record ObraRegistroDto(long Id,long? ObraId,string? Codigo,string? Nome,string? Descricao,string Status,DateTimeOffset? DataReferencia,decimal? Quantidade,decimal? Valor);
public sealed record ObraRegistroRequest(string? Codigo,string? Nome,string? Descricao,string Status="ATIVO",DateTimeOffset? DataReferencia=null,decimal? Quantidade=null,decimal? Valor=null,long? ObraId=null,long? ContratoId=null,string? Justificativa=null);
public sealed record ObrasDashboardDto(long ObrasAtivas,long ObrasAtrasadas,long MedicoesPendentes,long FiscalizacoesMes,decimal ValorMedido);
public interface IObrasRepository { Task<PagedResult<ObraRegistroDto>> ListarAsync(long tenantId,string recurso,long? obraId,int pagina,int tamanho,CancellationToken ct); Task<long> CriarAsync(long tenantId,long? entidadeId,long? exercicioId,long? usuarioId,string recurso,ObraRegistroRequest request,string correlationId,CancellationToken ct); Task<ObrasDashboardDto> DashboardAsync(long tenantId,CancellationToken ct); }
public interface IObrasService { Task<Result<PagedResult<ObraRegistroDto>>> ListarAsync(string recurso,long? obraId,int pagina,int tamanho,CancellationToken ct); Task<Result<long>> CriarAsync(string recurso,ObraRegistroRequest request,string correlationId,CancellationToken ct); Task<Result<ObrasDashboardDto>> DashboardAsync(CancellationToken ct); }
