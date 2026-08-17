using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Bloco8;

public sealed class Bloco8Service : IProcessosDigitaisService,IProtocoloDigitalService,IProcessoNumeroService,IProcessoMovimentacaoService,IProcessoRelatorioService,IGedService,IGedDocumentoService,IGedArquivoFisicoService,IGedValidacaoPublicaService,IAssinaturaService,ILegislativoService,ILegislativoProposicaoService,ILegislativoSessaoService,ILegislativoVotacaoService,ILegislativoNormaService,ILegislativoRelatorioService,ITransparenciaService,IDiarioOficialService,IEsicService,IOuvidoriaService,IAtendimentoDigitalService
{
 private readonly ICurrentTenant _tenant; private readonly ICurrentUser _user; private readonly IBloco8Repository _repository; private readonly IAuditService _audit;
 public Bloco8Service(ICurrentTenant tenant,ICurrentUser user,IBloco8Repository repository,IAuditService audit){_tenant=tenant;_user=user;_repository=repository;_audit=audit;}
 public async Task<Result<PagedResult<Bloco8RegistroDto>>> ListarAsync(string recurso,int pagina,int tamanho,CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<PagedResult<Bloco8RegistroDto>>.Failure("Tenant obrigatório.");return Result<PagedResult<Bloco8RegistroDto>>.Success(await _repository.ListarAsync(_tenant.TenantId.Value,recurso,Math.Max(1,pagina),Math.Clamp(tamanho,1,100),ct));}
 public async Task<Result<Bloco8RegistroDto>> ObterAsync(string recurso,long id,CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<Bloco8RegistroDto>.Failure("Tenant obrigatório.");var item=await _repository.ObterAsync(_tenant.TenantId.Value,recurso,id,ct);return item is null?Result<Bloco8RegistroDto>.Failure("Registro não encontrado."):Result<Bloco8RegistroDto>.Success(item);}
 public async Task<Result<long>> CriarAsync(string recurso,Bloco8RegistroRequest request,string correlationId,CancellationToken ct)
 {
  if(!_tenant.TenantId.HasValue)return Result<long>.Failure("Tenant obrigatório.");
  var validationError=ValidateCreation(recurso,request);
  if(validationError is not null)return Result<long>.Failure(validationError);
  var id=await _repository.CriarAsync(_tenant.TenantId.Value,_tenant.EntidadeId,_tenant.ExercicioId,_user.UsuarioId,recurso,request,correlationId,ct);
  await _audit.RegistrarAsync("bloco8","CRIAR",recurso,id.ToString(),null,new{id,request.Status,request.Sigiloso},ct);
  return Result<long>.Success(id);
 }
 public async Task<Result> AlterarStatusAsync(string recurso,long id,string status,string? justificativa,string correlationId,CancellationToken ct)
 {
  if(!_tenant.TenantId.HasValue)return Result.Failure("Tenant obrigatório.");
  if(!AllowedStatuses.Contains(status))return Result.Failure("Status inválido para o fluxo.");
  if(StatusRequiringReason.Contains(status)&&string.IsNullOrWhiteSpace(justificativa))return Result.Failure("Justificativa obrigatória.");
  var current=await _repository.ObterAsync(_tenant.TenantId.Value,recurso,id,ct);
  if(current is null)return Result.Failure("Registro não encontrado.");
  var ok=await _repository.AlterarStatusAsync(_tenant.TenantId.Value,recurso,id,status,justificativa,_user.UsuarioId,correlationId,ct);
  if(!ok)return Result.Failure("Transição não permitida.");
  await _audit.RegistrarAsync("bloco8","STATUS",recurso,id.ToString(),new{current.Status},new{status,justificativa},ct);
  return Result.Success();
 }
 public async Task<Result<Bloco8DashboardDto>> DashboardAsync(string recurso,CancellationToken ct){if(!_tenant.TenantId.HasValue)return Result<Bloco8DashboardDto>.Failure("Tenant obrigatório.");return Result<Bloco8DashboardDto>.Success(await _repository.DashboardAsync(_tenant.TenantId.Value,recurso,ct));}

 private static readonly HashSet<string> AllowedStatuses=new(StringComparer.Ordinal){"ATIVO","ABERTO","EM_ANALISE","EM_TRAMITACAO","PENDENTE","CONCLUIDO","ENCERRADO","REABERTO","CANCELADO","RECUSADO","ASSINADO","PUBLICADO"};
 private static readonly HashSet<string> StatusRequiringReason=new(StringComparer.Ordinal){"ENCERRADO","REABERTO","CANCELADO","RECUSADO"};
 private static string? ValidateCreation(string recurso,Bloco8RegistroRequest request)
 {
  if(string.IsNullOrWhiteSpace(request.Titulo)&&string.IsNullOrWhiteSpace(request.Descricao))return "Título ou descrição é obrigatório.";
  if(!AllowedStatuses.Contains(request.Status))return "Status inicial inválido.";
  if(recurso=="processo_digital"&&(string.IsNullOrWhiteSpace(request.Tipo)||request.ResponsavelId is null))return "Processo exige tipo e responsável.";
  if(recurso=="ged_documento"&&(string.IsNullOrWhiteSpace(request.Titulo)||string.IsNullOrWhiteSpace(request.Tipo)))return "Documento exige título, tipo e classificação nos dados.";
  if(recurso=="assinatura_documento"&&request.ReferenciaId is null)return "Assinatura exige referência ao documento GED.";
  if(recurso=="legislativo_proposicao"&&(string.IsNullOrWhiteSpace(request.Tipo)||string.IsNullOrWhiteSpace(request.Descricao)))return "Proposição exige tipo e ementa.";
  if((recurso=="esic_pedido"||recurso=="atendimento_digital_chamado")&&request.PrazoAt is null)return "Atendimento exige prazo parametrizado.";
  return null;
 }
}
