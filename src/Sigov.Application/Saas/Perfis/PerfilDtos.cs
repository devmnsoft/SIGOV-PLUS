namespace Sigov.Application.Saas.Perfis;

public sealed record SaasPerfilTemplateResponse(long Id, string Codigo, string Nome, string NivelBase, string? Descricao, IReadOnlyCollection<string> Permissoes, bool Ativo);
public sealed record CriarPerfisTenantPorTemplateRequest(long TenantId, IReadOnlyCollection<string> TemplatesCodigos, IReadOnlyCollection<string> ModulosContratados);
