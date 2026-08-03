using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.ComprasEmpresariais;

namespace Sigov.Web.Controllers;

[Authorize,Route("ComprasEmpresariais")]
public sealed class ComprasEmpresariaisController(IFornecedorApplicationService fornecedores,IRequisicaoCompraApplicationService requisicoes,IComprasDashboardApplicationService dashboard):Controller
{
 private ComprasContext Contexto(){if(!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value??User.FindFirst("tenant_id")?.Value,out var t)||!Guid.TryParse(User.FindFirst("sub")?.Value,out var u))throw new UnauthorizedAccessException("Tenant e usuário não resolvidos.");return new(t,u,HttpContext.TraceIdentifier);}
 [HttpGet(""),Authorize(Policy="compras_empresariais.dashboard.visualizar")]public async Task<IActionResult> Index(CancellationToken ct)=>View(await dashboard.ObterAsync(Contexto(),ct));
 [HttpGet("Fornecedores"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedores(string? busca,string? status,int pagina=1,CancellationToken ct=default)=>View("Fornecedores/Index",await fornecedores.ListarAsync(Contexto(),new(busca,status,pagina,20),ct));
 [HttpGet("Fornecedores/Novo"),Authorize(Policy="compras_empresariais.fornecedores.criar")]public IActionResult NovoFornecedor()=>View("Fornecedores/Novo");
 [HttpGet("Fornecedores/{id:guid}"),Authorize(Policy="compras_empresariais.fornecedores.visualizar")]public async Task<IActionResult> Fornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Fornecedores/{id:guid}/Editar"),Authorize(Policy="compras_empresariais.fornecedores.editar")]public async Task<IActionResult> EditarFornecedor(Guid id,CancellationToken ct){var item=await fornecedores.ObterAsync(Contexto(),id,ct);return item is null?NotFound():View("Fornecedores/Detalhe",item);}
 [HttpGet("Requisicoes"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public async Task<IActionResult> Requisicoes(int pagina=1,CancellationToken ct=default)=>View("Requisicoes/Index",await requisicoes.ListarAsync(Contexto(),pagina,20,ct));
 [HttpGet("Requisicoes/Nova"),Authorize(Policy="compras_empresariais.requisicoes.criar")]public IActionResult NovaRequisicao()=>View("Requisicoes/Nova");
 [HttpGet("Requisicoes/{id:guid}"),Authorize(Policy="compras_empresariais.requisicoes.visualizar")]public IActionResult Requisicao(Guid id)=>View("Requisicoes/Detalhe",id);
 [HttpGet("Aprovacoes"),Authorize(Policy="compras_empresariais.aprovacoes.visualizar")]public IActionResult Aprovacoes()=>Workspace("Aprovações","Minhas aprovações e decisões por alçada.");
 [HttpGet("Cotacoes"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Cotacoes()=>Workspace("Cotações","Rodadas, convites e respostas de fornecedores.");
 [HttpGet("Cotacoes/Nova"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult NovaCotacao()=>Workspace("Nova cotação","Configure itens, prazo e fornecedores convidados.");
 [HttpGet("Cotacoes/{id:guid}"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Cotacao(Guid id)=>Workspace("Cotação","Workspace da cotação.");
 [HttpGet("Cotacoes/{id:guid}/Comparativo"),Authorize(Policy="compras_empresariais.cotacoes.visualizar")]public IActionResult Comparativo(Guid id)=>Workspace("Mapa comparativo","Julgamento humano por item e fornecedor.");
 [HttpGet("Pedidos"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public IActionResult Pedidos()=>Workspace("Pedidos","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Pedidos/{id:guid}"),Authorize(Policy="compras_empresariais.pedidos.visualizar")]public IActionResult Pedido(Guid id)=>Workspace("Pedidos","Detalhe 360, histórico e ações autorizadas.");
 [HttpGet("Recebimentos"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public IActionResult Recebimentos()=>Workspace("Recebimentos","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Recebimentos/{id:guid}"),Authorize(Policy="compras_empresariais.recebimentos.visualizar")]public IActionResult Recebimento(Guid id)=>Workspace("Recebimentos","Detalhe 360, histórico e ações autorizadas.");
 [HttpGet("Recebimentos/Novo"),Authorize(Policy="compras_empresariais.recebimentos.registrar")]public IActionResult NovoRecebimento()=>Workspace("Novo recebimento","Wizard de conferência, inspeção e evidências.");
 [HttpGet("Faturas"),Authorize(Policy="compras_empresariais.faturas.visualizar")]public IActionResult Faturas()=>Workspace("Faturas","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Faturas/{id:guid}"),Authorize(Policy="compras_empresariais.faturas.visualizar")]public IActionResult Fatura(Guid id)=>Workspace("Faturas","Detalhe 360, histórico e ações autorizadas.");
 [HttpGet("Devolucoes"),Authorize(Policy="compras_empresariais.devolucoes.visualizar")]public IActionResult Devolucoes()=>Workspace("Devoluções","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Avaliacoes"),Authorize(Policy="compras_empresariais.avaliacoes.gerenciar")]public IActionResult Avaliacoes()=>Workspace("Avaliações","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Relatorios"),Authorize(Policy="compras_empresariais.relatorios.visualizar")]public IActionResult Relatorios()=>Workspace("Relatórios","Operação integrada à jornada procure-to-pay.");
 [HttpGet("Configuracao"),Authorize(Policy="compras_empresariais.configuracao.gerenciar")]public IActionResult Configuracao()=>Workspace("Configuração","Operação integrada à jornada procure-to-pay.");
 private IActionResult Workspace(string title,string description){ViewData["Title"]=title;ViewData["Description"]=description;return View("Workspace");}
}
