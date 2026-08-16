using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Obras;
using Sigov.Application.Saas;
using Sigov.Domain.Common;

namespace Sigov.Application.Frotas;

public sealed class FrotasService : IFrotasService
{
    private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user; private readonly IFrotasRepository _repository; private readonly IAuditService _audit;
    public FrotasService(ICurrentTenant tenant,ICurrentUser user,IFrotasRepository repository,IAuditService audit){_tenant=tenant;_user=user;_repository=repository;_audit=audit;}
    private Result<long> Tenant()=>_tenant.TenantId.HasValue?Result<long>.Success(_tenant.TenantId.Value):Result<long>.Failure("Tenant obrigatório.");
    public async Task<Result<PagedResult<FrotaRegistroDto>>> ListarAsync(string recurso,int pagina,int tamanho,CancellationToken ct){var t=Tenant();if(t.IsFailure)return Result<PagedResult<FrotaRegistroDto>>.Failure(t.Error!);return Result<PagedResult<FrotaRegistroDto>>.Success(await _repository.ListarAsync(t.Value,Recurso(recurso),Math.Max(1,pagina),Math.Clamp(tamanho,1,100),ct));}
    public async Task<Result<long>> CriarAsync(string recurso,FrotaRegistroRequest r,string correlationId,CancellationToken ct){var t=Tenant();if(t.IsFailure)return Result<long>.Failure(t.Error!);if(string.IsNullOrWhiteSpace(r.Nome)&&string.IsNullOrWhiteSpace(r.Descricao))return Result<long>.Failure("Nome ou descrição é obrigatório.");if((recurso=="abastecimento")&&(r.VeiculoId is null||r.Quantidade is null or <=0||r.Valor is null or <0))return Result<long>.Failure("Abastecimento exige veículo, quantidade e valor válidos.");var id=await _repository.CriarAsync(t.Value,_tenant.EntidadeId,_tenant.ExercicioId,_user.UsuarioId,Recurso(recurso),r,correlationId,ct);await _audit.RegistrarAsync("frotas","CRIAR",Recurso(recurso),id.ToString(),null,new{id},ct);return Result<long>.Success(id);}
    public async Task<Result<FrotasDashboardDto>> DashboardAsync(CancellationToken ct){var t=Tenant();return t.IsFailure?Result<FrotasDashboardDto>.Failure(t.Error!):Result<FrotasDashboardDto>.Success(await _repository.DashboardAsync(t.Value,ct));}
    private static string Recurso(string r)=>r switch{"veiculos" or "veiculo"=>"frota_veiculo","motoristas" or "motorista"=>"frota_motorista","abastecimentos" or "abastecimento"=>"frota_abastecimento","manutencoes" or "manutencao"=>"frota_manutencao","viagens" or "viagem"=>"frota_viagem",_=>throw new ArgumentException("Recurso de frota inválido.")};
}

public sealed class ObrasService : IObrasService
{
    private readonly ICurrentTenant _tenant;private readonly ICurrentUser _user;private readonly IObrasRepository _repository;private readonly IAuditService _audit;
    public ObrasService(ICurrentTenant tenant,ICurrentUser user,IObrasRepository repository,IAuditService audit){_tenant=tenant;_user=user;_repository=repository;_audit=audit;}
    public async Task<Result<PagedResult<ObraRegistroDto>>> ListarAsync(string recurso,long? obraId,int pagina,int tamanho,CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<PagedResult<ObraRegistroDto>>.Failure("Tenant obrigatório.");return Result<PagedResult<ObraRegistroDto>>.Success(await _repository.ListarAsync(_tenant.TenantId.Value,Recurso(recurso),obraId,Math.Max(1,pagina),Math.Clamp(tamanho,1,100),ct));}
    public async Task<Result<long>> CriarAsync(string recurso,ObraRegistroRequest r,string correlationId,CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<long>.Failure("Tenant obrigatório.");if(string.IsNullOrWhiteSpace(r.Nome)&&string.IsNullOrWhiteSpace(r.Descricao))return Result<long>.Failure("Objeto ou descrição é obrigatório.");var id=await _repository.CriarAsync(_tenant.TenantId.Value,_tenant.EntidadeId,_tenant.ExercicioId,_user.UsuarioId,Recurso(recurso),r,correlationId,ct);await _audit.RegistrarAsync("obras","CRIAR",Recurso(recurso),id.ToString(),null,new{id},ct);return Result<long>.Success(id);}
    public async Task<Result<ObrasDashboardDto>> DashboardAsync(CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<ObrasDashboardDto>.Failure("Tenant obrigatório.");return Result<ObrasDashboardDto>.Success(await _repository.DashboardAsync(_tenant.TenantId.Value,ct));}
    private static string Recurso(string r)=>r switch{"obras" or "obra"=>"obra","etapas" or "etapa"=>"obra_etapa","medicoes" or "medicao"=>"obra_medicao","fiscalizacoes" or "fiscalizacao"=>"obra_fiscalizacao","diario"=>"obra_diario",_=>throw new ArgumentException("Recurso de obra inválido.")};
}
