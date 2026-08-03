using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.ComprasEmpresariais;

namespace Sigov.Api.Controllers.ComprasEmpresariais;

[ApiController,Authorize,Route("api/compras-empresariais")]
public sealed class ComprasEmpresariaisController(IFornecedorApplicationService fornecedores,IRequisicaoCompraApplicationService requisicoes,IComprasDashboardApplicationService dashboard):ControllerBase
{
 private ComprasContext Contexto(){if(!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value??User.FindFirst("tenant_id")?.Value,out var t)||t==Guid.Empty)throw new UnauthorizedAccessException("Tenant não resolvido.");if(!Guid.TryParse(User.FindFirst("sub")?.Value,out var u)||u==Guid.Empty)throw new UnauthorizedAccessException("Usuário não resolvido.");return new(t,u,HttpContext.TraceIdentifier);}
 private string Key()=>Request.Headers["Idempotency-Key"].ToString();
 [HttpGet("dashboard"),Authorize(Policy="compras_empresariais.dashboard.visualizar")]public async Task<IActionResult> Dashboard(CancellationToken ct)=>Ok(await dashboard.ObterAsync(Contexto(),ct));
 [HttpGet("fornecedores"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedores([FromQuery]FornecedorFiltro filtro,CancellationToken ct)=>Ok(await fornecedores.ListarAsync(Contexto(),filtro,ct));
 [HttpGet("fornecedores/{id:guid}"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():Ok(item);}
 [HttpPost("fornecedores"),Authorize(Policy="compras_empresariais.fornecedores.criar")]public async Task<IActionResult> CriarFornecedor(CriarFornecedorRequest request,CancellationToken ct){var id=await fornecedores.CriarAsync(Contexto(),request,Key(),ct);return CreatedAtAction(nameof(Fornecedor),new{id},new{id});}
 [HttpPost("fornecedores/{id:guid}/status"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> Status(Guid id,AlterarStatusRequest request,CancellationToken ct){await fornecedores.AlterarStatusAsync(Contexto(),id,request,ct);return NoContent();}
 [HttpPost("fornecedores/{id:guid}/contatos"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> Contato(Guid id,AdicionarContatoRequest request,CancellationToken ct){await fornecedores.AdicionarContatoAsync(Contexto(),id,request,Key(),ct);return NoContent();}
 [HttpPost("fornecedores/{id:guid}/enderecos"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> Endereco(Guid id,AdicionarEnderecoRequest request,CancellationToken ct){await fornecedores.AdicionarEnderecoAsync(Contexto(),id,request,Key(),ct);return NoContent();}
 [HttpPost("fornecedores/{id:guid}/documentos"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> Documento(Guid id,AdicionarDocumentoRequest request,CancellationToken ct){await fornecedores.AdicionarDocumentoAsync(Contexto(),id,request,Key(),ct);return NoContent();}
 [HttpGet("requisicoes"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicoes(int pagina=1,int tamanho=20,CancellationToken ct=default)=>Ok(await requisicoes.ListarAsync(Contexto(),pagina,tamanho,ct));
 [HttpPost("requisicoes"),Authorize(Policy="compras_empresariais.requisicoes.criar")]public async Task<IActionResult> CriarRequisicao(CriarRequisicaoRequest request,CancellationToken ct){var id=await requisicoes.CriarAsync(Contexto(),request,Key(),ct);return Created($"/api/compras-empresariais/requisicoes/{id}",new{id});}
 [HttpPost("requisicoes/{id:guid}/enviar"),Authorize(Policy="compras_empresariais.requisicoes.enviar")]public async Task<IActionResult> Enviar(Guid id,[FromQuery]long version,CancellationToken ct){await requisicoes.EnviarAsync(Contexto(),id,version,ct);return NoContent();}
}
