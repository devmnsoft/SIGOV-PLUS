namespace Sigov.Application.Saas;

public sealed record TenantInfo(long Id, string Nome, string Slug, string Status, string Ambiente);
public sealed record TenantResolutionResult(bool Resolved, TenantInfo? Tenant, string? Reason);
public sealed record TenantModuleInfo(string Codigo, string Nome, bool Contratado, bool Habilitado);
public sealed record TenantFeatureInfo(string Codigo, bool Habilitado, string ValorJson);
public sealed record ProvisionTenantRequest(string NomeTenant, string? Documento, string Slug, string? Dominio, string PlanoCodigo, string NomeEntidade, string? CnpjEntidade, int AnoExercicio, string AdminNome, string AdminEmail, string AdminLogin, IReadOnlyCollection<string>? Modulos, string Ambiente);
public sealed record ProvisionTenantResult(long TenantId, string Slug, long? EntidadeId, long? ExercicioId, long? UsuarioAdminId, bool SenhaTemporariaGerada);
