using System.Security.Claims;
using Sigov.Web.Models.Busca;

namespace Sigov.Web.Services;

public sealed class BuscaGlobalService
{
    private static readonly BuscaSugestaoViewModel[] Fallback =
    {
        new("Workspace", "Minha Central", "Home operacional com pendências, favoritos e recentes.", "/MinhaCentral", "home", "Home", "Ctrl+H"),
        new("Executivo", "Dashboard", "KPIs SaaS, implantação, LGPD, auditoria e módulos.", "/Dashboard", "dashboard", "BI", "Ctrl+D"),
        new("Governo", "Protocolo", "Acompanhar processos, prazos e tramitações.", "/Protocolo", "protocol", "Processos", "P"),
        new("Governo", "GED/OCR", "Documentos, OCR, assinaturas e pesquisa segura.", "/Ged/Dashboard", "documents", "GED", "G"),
        new("Administração", "Usuários", "Gestão de operadores, perfis e permissões.", "/Seguranca/Usuarios", "users", "Admin", "U"),
        new("Segurança", "LGPD", "Alertas de privacidade e dados pessoais.", "/Lgpd/Dashboard", "shield", "LGPD", "L"),
        new("Operação", "Health", "Saúde operacional, banco, API e outbox.", "/Operacao/Health", "active", "Ops", "H"),
        new("Ação rápida", "Novo", "Abrir modal de criação rápida acessível.", "#quick-create", "plus", "Criar", "N")
    };

    private readonly ILogger<BuscaGlobalService> _logger;
    private readonly IUserPermissionService _permissions;

    public BuscaGlobalService(ILogger<BuscaGlobalService> logger, IUserPermissionService permissions)
    {
        _logger = logger;
        _permissions = permissions;
    }

    public Task<IReadOnlyCollection<BuscaSugestaoViewModel>> SugerirAsync(string? query, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var termo = (query ?? string.Empty).Trim();
            var resultados = string.IsNullOrWhiteSpace(termo)
                ? Fallback
                : Fallback.Where(x => (x.Area + " " + x.Titulo + " " + x.Descricao + " " + x.Badge).Contains(termo, StringComparison.OrdinalIgnoreCase)).ToArray();

            // RC-EVO A: visibilidade da sugestão /Seguranca segue o mesmo critério do
            // SegurancaController (permissões efetivas do snapshot, não claim de role).
            var podeSeguranca = _permissions.HasPermission(user, "saas.plataforma.administrar") || _permissions.HasPermission(user, "cliente.usuarios.gerenciar");
            if (!podeSeguranca)
            {
                resultados = resultados.Where(x => !x.Url.StartsWith("/Seguranca", StringComparison.OrdinalIgnoreCase)).ToArray();
            }

            return Task.FromResult<IReadOnlyCollection<BuscaSugestaoViewModel>>(resultados.Take(12).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Falha ao montar sugestões globais seguras.");
            return Task.FromResult<IReadOnlyCollection<BuscaSugestaoViewModel>>(Fallback.Take(8).ToArray());
        }
    }
}
