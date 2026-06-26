using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Models.Lote1;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SegurancaController : Controller
{
    private readonly SegurancaAdminService _service;
    private readonly ILogger<SegurancaController> _logger;
    public SegurancaController(SegurancaAdminService service, ILogger<SegurancaController> logger){ _service=service; _logger=logger; }

    [HttpGet]
    public async Task<IActionResult> Usuarios([FromQuery] UsuarioFiltroViewModel filtro, CancellationToken ct)
    { var usuarios=await _service.ListarUsuariosAsync(filtro,ct).ConfigureAwait(false); return View(new UsuariosAdminViewModel{Filtro=filtro,Usuarios=usuarios,MensagemFallback=usuarios.Any()?string.Empty:"Nenhum usuário retornado ou tabela indisponível; nenhum dado foi simulado."}); }

    [HttpGet("Seguranca/Usuarios/Novo")]
    public IActionResult NovoUsuario()=>View("Usuarios", new UsuariosAdminViewModel{Form=new UsuarioFormViewModel(), MensagemFallback="Preencha os dados e salve para persistir em sigov.usuario."});

    [HttpPost("Seguranca/Usuarios/Novo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoUsuario(UsuarioFormViewModel form, CancellationToken ct)=>await SalvarUsuarioPost(form,ct).ConfigureAwait(false);

    [HttpGet("Seguranca/Usuarios/{id:long}")]
    public async Task<IActionResult> UsuarioDetalhe(long id, CancellationToken ct){ var vm=await _service.ObterUsuarioAsync(id,ct).ConfigureAwait(false); return View(vm ?? new UsuarioDetalheViewModel{Id=id,MensagemFallback="Usuário não encontrado ou estrutura indisponível."}); }

    [HttpGet("Seguranca/Usuarios/{id:long}/Editar")]
    public async Task<IActionResult> EditarUsuario(long id, CancellationToken ct){ var vm=await _service.ObterUsuarioAsync(id,ct).ConfigureAwait(false); if(vm is null){TempData["Error"]="Usuário não encontrado."; return RedirectToAction(nameof(Usuarios));} return View("Usuarios", new UsuariosAdminViewModel{Form=vm, Usuarios=await _service.ListarUsuariosAsync(new UsuarioFiltroViewModel(),ct).ConfigureAwait(false)}); }

    [HttpPost("Seguranca/Usuarios/{id:long}/Editar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarUsuario(long id, UsuarioFormViewModel form, CancellationToken ct){ form.Id=id; return await SalvarUsuarioPost(form,ct).ConfigureAwait(false); }

    [HttpPost("Seguranca/Usuarios/{id:long}/Inativar")][ValidateAntiForgeryToken]
    public async Task<IActionResult> InativarUsuario(long id,CancellationToken ct)=>await Status(id,false,ct).ConfigureAwait(false);
    [HttpPost("Seguranca/Usuarios/{id:long}/Ativar")][ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarUsuario(long id,CancellationToken ct)=>await Status(id,true,ct).ConfigureAwait(false);
    [HttpPost("Seguranca/Usuarios/{id:long}/ResetSenha")][ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetSenha(long id,CancellationToken ct){ var ok=await _service.ResetarSenhaAsync(id,ct).ConfigureAwait(false); TempData[ok?"Success":"Error"]=ok?"Senha resetada; troca obrigatória ativada e auditoria preparada.":"Não foi possível resetar; nenhum sucesso foi simulado."; return RedirectToAction(nameof(Usuarios)); }

    [HttpGet]
    public async Task<IActionResult> Perfis(CancellationToken ct){ var perfis=await _service.ListarPerfisAsync(ct).ConfigureAwait(false); return View(new PerfisAdminViewModel{Perfis=perfis,MensagemFallback=perfis.Any()?string.Empty:"Tabela de perfis indisponível; cadastro não será simulado."}); }
    [HttpGet("Seguranca/Perfis/Novo")]
    public async Task<IActionResult> NovoPerfil(CancellationToken ct) => View("Perfis", new PerfisAdminViewModel{Perfis=await _service.ListarPerfisAsync(ct).ConfigureAwait(false)});

    [HttpPost("Seguranca/Perfis/Novo")][ValidateAntiForgeryToken]
    public async Task<IActionResult> NovoPerfil(PerfilFormViewModel form,CancellationToken ct){ if(!ModelState.IsValid){TempData["Error"]="Informe código e nome do perfil."; return RedirectToAction(nameof(Perfis));} var ok=await _service.CriarPerfilAsync(form,ct).ConfigureAwait(false); TempData[ok?"Success":"Error"]=ok?"Perfil salvo e auditado.":"Não foi possível salvar perfil; verifique duplicidade ou estrutura indisponível."; return RedirectToAction(nameof(Perfis)); }

    [HttpGet("Seguranca/Perfis/{id:long}/Editar")]
    public async Task<IActionResult> EditarPerfil(long id, CancellationToken ct){ var perfil=await _service.ObterPerfilAsync(id,ct).ConfigureAwait(false); if(perfil is null){TempData["Error"]="Perfil não encontrado."; return RedirectToAction(nameof(Perfis));} return View("Perfis", new PerfisAdminViewModel{Form=perfil, Perfis=await _service.ListarPerfisAsync(ct).ConfigureAwait(false)}); }

    [HttpPost("Seguranca/Perfis/{id:long}/Editar")][ValidateAntiForgeryToken]
    public async Task<IActionResult> EditarPerfil(long id, PerfilFormViewModel form, CancellationToken ct){ if(!ModelState.IsValid){TempData["Error"]="Informe código e nome do perfil."; return RedirectToAction(nameof(Perfis));} var ok=await _service.AtualizarPerfilAsync(id,form,ct).ConfigureAwait(false); TempData[ok?"Success":"Error"]=ok?"Perfil atualizado e auditado.":"Perfil não foi persistido; nenhum sucesso foi simulado."; return RedirectToAction(nameof(Perfis)); }

    [HttpPost("Seguranca/Perfis/{id:long}/Ativar")][ValidateAntiForgeryToken]
    public async Task<IActionResult> AtivarPerfil(long id, CancellationToken ct)=>await StatusPerfil(id,true,ct).ConfigureAwait(false);

    [HttpPost("Seguranca/Perfis/{id:long}/Inativar")][ValidateAntiForgeryToken]
    public async Task<IActionResult> InativarPerfil(long id, CancellationToken ct)=>await StatusPerfil(id,false,ct).ConfigureAwait(false);

    [HttpGet("Seguranca/Perfis/{id:long}/Permissoes")]
    public async Task<IActionResult> PermissoesPerfil(long id, CancellationToken ct)
    {
        var vm = await _service.ObterPermissoesPerfilAsync(id, ct).ConfigureAwait(false);
        return View("Permissoes", vm);
    }

    [HttpPost("Seguranca/Perfis/{id:long}/Permissoes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PermissoesPerfil(long id, long[] permissaoIds, CancellationToken ct)
    {
        var ok = await _service.SalvarPermissoesPerfilAsync(id, permissaoIds ?? Array.Empty<long>(), ct).ConfigureAwait(false);
        TempData[ok ? "Success" : "Error"] = ok
            ? "Permissões do perfil salvas em transação e auditadas."
            : "Permissões não foram persistidas; estrutura indisponível ou erro controlado.";
        return RedirectToAction(nameof(PermissoesPerfil), new { id });
    }

    [HttpGet]
    public IActionResult Permissoes() => View(new PerfilPermissoesViewModel { MensagemFallback = "Selecione um perfil em Segurança > Perfis para editar permissões reais. Esta tela não simula salvamento genérico." });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissoes(CancellationToken ct)
    {
        var ok = await _service.SalvarPermissoesAsync(ct).ConfigureAwait(false);
        TempData[ok ? "Success" : "Warning"] = ok
            ? "Permissões salvas e auditadas."
            : "Estrutura definitiva de permissões indisponível; nenhuma alteração foi simulada.";
        return RedirectToAction(nameof(Permissoes));
    }
    public IActionResult Grupos() => View(new GrupoFormViewModel());
    public IActionResult HistoricoLogin() => View();

    private async Task<IActionResult> SalvarUsuarioPost(UsuarioFormViewModel form,CancellationToken ct){ if(!ModelState.IsValid){TempData["Error"]="Corrija os campos obrigatórios."; return RedirectToAction(nameof(Usuarios));} var r=await _service.SalvarUsuarioAsync(form,ct).ConfigureAwait(false); TempData[r.Ok?"Success":"Error"]=r.Mensagem; return RedirectToAction(nameof(Usuarios)); }
    private async Task<IActionResult> Status(long id,bool ativo,CancellationToken ct){ var ok=await _service.AlterarStatusUsuarioAsync(id,ativo,ct).ConfigureAwait(false); TempData[ok?"Success":"Error"]=ok?(ativo?"Usuário ativado e auditado.":"Usuário inativado e auditado."):"Ação não persistida; nenhum sucesso foi simulado."; return RedirectToAction(nameof(Usuarios)); }
    private async Task<IActionResult> StatusPerfil(long id,bool ativo,CancellationToken ct){ var ok=await _service.AlterarStatusPerfilAsync(id,ativo,ct).ConfigureAwait(false); TempData[ok?"Success":"Error"]=ok?(ativo?"Perfil ativado e auditado.":"Perfil inativado e auditado."):"Perfil não foi alterado; nenhum sucesso foi simulado."; return RedirectToAction(nameof(Perfis)); }
}
