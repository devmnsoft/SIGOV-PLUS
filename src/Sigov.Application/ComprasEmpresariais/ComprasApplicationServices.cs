namespace Sigov.Application.ComprasEmpresariais;

internal static class ComprasGuard
{
 public static void Context(ComprasContext c){if(c.TenantId==Guid.Empty||c.UsuarioId==Guid.Empty)throw new UnauthorizedAccessException("Tenant e usuário autenticados são obrigatórios.");}
 public static void Key(string key){if(string.IsNullOrWhiteSpace(key)||key.Length>200)throw new ArgumentException("Idempotency-Key válida é obrigatória.");}
}
public sealed class FornecedorApplicationService(IFornecedorRepository repository):IFornecedorApplicationService
{
 public Task<Common.PagedResult<FornecedorResumo>> ListarAsync(ComprasContext c,FornecedorFiltro f,CancellationToken ct){ComprasGuard.Context(c);return repository.ListarAsync(c.TenantId,f,ct);}
 public Task<FornecedorResumo?> ObterAsync(ComprasContext c,Guid id,CancellationToken ct){ComprasGuard.Context(c);return repository.ObterAsync(c.TenantId,id,ct);}
 public Task<Guid> CriarAsync(ComprasContext c,CriarFornecedorRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);if(string.IsNullOrWhiteSpace(r.Documento)||string.IsNullOrWhiteSpace(r.RazaoSocial))throw new ArgumentException("Documento e razão social são obrigatórios.");return repository.CriarAsync(c,r,key,ct);}
 public Task AlterarStatusAsync(ComprasContext c,Guid id,AlterarStatusRequest r,CancellationToken ct){ComprasGuard.Context(c);return repository.AlterarStatusAsync(c,id,r,ct);}
 public Task AdicionarContatoAsync(ComprasContext c,Guid id,AdicionarContatoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarContatoAsync(c,id,r,key,ct);}
 public Task AdicionarEnderecoAsync(ComprasContext c,Guid id,AdicionarEnderecoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarEnderecoAsync(c,id,r,key,ct);}
 public Task AdicionarDocumentoAsync(ComprasContext c,Guid id,AdicionarDocumentoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);return repository.AdicionarDocumentoAsync(c,id,r,key,ct);}
}
public sealed class RequisicaoCompraApplicationService(IRequisicaoCompraRepository repository):IRequisicaoCompraApplicationService
{
 public Task<Common.PagedResult<RequisicaoResumo>> ListarAsync(ComprasContext c,int p,int s,CancellationToken ct){ComprasGuard.Context(c);return repository.ListarAsync(c.TenantId,p,s,ct);}
 public Task<Guid> CriarAsync(ComprasContext c,CriarRequisicaoRequest r,string key,CancellationToken ct){ComprasGuard.Context(c);ComprasGuard.Key(key);if(r.Itens.Count==0)throw new ArgumentException("Inclua ao menos um item.");if(r.Itens.Any(x=>x.Quantidade<=0||x.ValorEstimado<0))throw new ArgumentException("Quantidade e valor dos itens são inválidos.");return repository.CriarAsync(c,r,key,ct);}
 public Task EnviarAsync(ComprasContext c,Guid id,long version,CancellationToken ct){ComprasGuard.Context(c);return repository.EnviarAsync(c,id,version,ct);}
}
public sealed class ComprasDashboardApplicationService(IComprasDashboardRepository repository):IComprasDashboardApplicationService
{ public Task<ComprasDashboard> ObterAsync(ComprasContext c,CancellationToken ct){ComprasGuard.Context(c);return repository.ObterAsync(c.TenantId,ct);} }
