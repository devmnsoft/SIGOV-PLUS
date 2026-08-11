using System.Text.Json;
using Dapper;
using Sigov.Application.Abstractions;
using Sigov.Application.Security;
using Sigov.Infrastructure.Diagnostics;
using Sigov.Infrastructure.Persistence.Dapper;

namespace Sigov.Web.Services.Development;

public sealed record DevelopmentAuthReport(
    DateTimeOffset CheckedAt, string Environment, SafeDatabaseTarget ConnectionTarget,
    int AdminUserCount, long? CanonicalAdminId, int DuplicateAdminsHandled,
    bool PasswordMatches, int RolesCount, int PermissionsCount, bool HasGroup,
    bool HasProfile, bool HasEntity, bool HasExercise, string FinalReason,
    bool ResetPerformed, string LoginSmokeResult, DevelopmentAdmin? Admin = null,
    string? Error = null);

public sealed record DevelopmentAdmin(long Id, long? TenantId, string Login, string Email,
    string Nome, bool Ativo, bool Bloqueado, bool IsDeleted, bool DeveAlterarSenha,
    string TipoUsuario, bool TenantAtivo, bool TenantIsDeleted, bool HashExists,
    string HashPrefix, bool HashFormatValid);

public sealed class DevelopmentAuthDiagnosticService
{
    public const string AdminPassword = "SigovDevLocal!2026";
    private readonly NpgsqlConnectionFactory _connections;
    private readonly IPasswordHashService _passwords;
    private readonly IAuthenticationRepository _authentication;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<DevelopmentAuthDiagnosticService> _logger;

    public DevelopmentAuthDiagnosticService(NpgsqlConnectionFactory connections, IPasswordHashService passwords,
        IAuthenticationRepository authentication, IConfiguration configuration, IWebHostEnvironment environment,
        ILogger<DevelopmentAuthDiagnosticService> logger)
    {
        _connections = connections; _passwords = passwords; _authentication = authentication;
        _configuration = configuration; _environment = environment; _logger = logger;
    }

    public async Task<DevelopmentAuthReport> DiagnoseAsync(bool resetPerformed = false, int duplicatesHandled = 0, CancellationToken ct = default)
    {
        var target = SafeConnectionStringDiagnostics.Read(_configuration, _environment);
        try
        {
            using var connection = _connections.CreateConnection();
            const string sql = @"select u.id, u.tenant_id as TenantId, u.login, u.email, coalesce(u.nome,u.login) as Nome,
 u.ativo, u.bloqueado, u.is_deleted as IsDeleted, coalesce(u.deve_alterar_senha,false) as DeveAlterarSenha,
 coalesce(u.tipo_usuario,'') as TipoUsuario, coalesce(t.ativo,true) as TenantAtivo,
 coalesce(t.is_deleted,false) as TenantIsDeleted, u.senha_hash as PasswordHash,
 exists(select 1 from sigov.usuario_grupo x where x.usuario_id=u.id and not x.is_deleted) as HasGroup,
 exists(select 1 from sigov.usuario_grupo x join sigov.grupo_perfil gp on gp.grupo_acesso_id=x.grupo_acesso_id and not gp.is_deleted where x.usuario_id=u.id and not x.is_deleted) as HasProfile,
 exists(select 1 from sigov.usuario_entidade x where x.usuario_id=u.id and x.ativo) as HasEntity,
 exists(select 1 from sigov.usuario_exercicio x where x.usuario_id=u.id and x.ativo) as HasExercise
from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
where lower(u.login)='admin' or lower(u.email)='admin@sigov.local'
order by u.is_deleted, u.ativo desc, u.bloqueado, u.id;";
            var rows = (await connection.QueryAsync<AdminRow>(new CommandDefinition(sql, cancellationToken: ct))).ToArray();
            var row = rows.FirstOrDefault();
            var repositoryUser = await _authentication.FindForLoginAsync("admin", ct);
            var access = repositoryUser is null ? new AuthenticationAccess(Array.Empty<string>(), Array.Empty<string>()) : await _authentication.GetAccessAsync(repositoryUser.Id, ct);
            var validFormat = IsValidHash(row?.PasswordHash);
            var matches = validFormat && _passwords.VerifyPassword(AdminPassword, row!.PasswordHash);
            var reason = Reason(rows.Length, row, repositoryUser, validFormat, matches, access);
            var admin = row is null ? null : new DevelopmentAdmin(row.Id, row.TenantId, row.Login, row.Email, row.Nome,
                row.Ativo, row.Bloqueado, row.IsDeleted, row.DeveAlterarSenha, row.TipoUsuario, row.TenantAtivo,
                row.TenantIsDeleted, !string.IsNullOrEmpty(row.PasswordHash), HashPrefix(row.PasswordHash), validFormat);
            var report = new DevelopmentAuthReport(DateTimeOffset.UtcNow, _environment.EnvironmentName, target, rows.Length,
                row?.Id, duplicatesHandled, matches, access.Roles.Count, access.Permissions.Count, row?.HasGroup == true,
                row?.HasProfile == true, row?.HasEntity == true, row?.HasExercise == true, reason, resetPerformed,
                reason == "OK" ? "OK (cookie não criado pelo teste)" : reason, admin);
            await WriteReportAsync(report, ct);
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no diagnóstico interno de autenticação. CorrelationId={CorrelationId}", System.Diagnostics.Activity.Current?.Id);
            var report = new DevelopmentAuthReport(DateTimeOffset.UtcNow, _environment.EnvironmentName, target, 0, null, 0,
                false, 0, 0, false, false, false, false, "DATABASE_ERROR", resetPerformed, "DATABASE_ERROR", Error: "Consulte o log pelo CorrelationId.");
            await WriteReportAsync(report, ct);
            return report;
        }
    }

    public async Task<DevelopmentAuthReport> ResetAdminAsync(string correlationId, string? ip, string? userAgent, CancellationToken ct)
    {
        var guardPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..",
            "database", "postgres", "seeds", "development", "999_super_admin_access_guard.sql"));
        if (!File.Exists(guardPath)) throw new FileNotFoundException("Guard administrativo Development não encontrado.", guardPath);

        var guardSql = await File.ReadAllTextAsync(guardPath, ct);
        using var connection = _connections.CreateConnection();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("set local sigov.environment = 'DEVELOPMENT';\n" + guardSql,
            transaction: transaction, cancellationToken: ct));
        var adminId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "select id from sigov.usuario where login='admin' and not is_deleted order by id desc limit 1",
            transaction: transaction, cancellationToken: ct));
        const string auditSql = @"insert into sigov.auditoria_evento
    (tenant_id, usuario_id, acao, entidade, entidade_id, depois, ip, user_agent, correlation_id, created_at)
select tenant_id,id,'DEV_ADMIN_ACCESS_GUARD','sigov.usuario',id::text,
       jsonb_build_object('resetPerformed', true),@Ip,@UserAgent,cast(@CorrelationId as uuid),now()
from sigov.usuario where id=@Id;";
        await connection.ExecuteAsync(new CommandDefinition(auditSql,
            new { Id = adminId, Ip = ip, UserAgent = userAgent, CorrelationId = correlationId },
            transaction, cancellationToken: ct));
        await transaction.CommitAsync(ct);
        _logger.LogWarning("DEV_ADMIN_ACCESS_GUARD concluído. UserId={UserId}; CorrelationId={CorrelationId}", adminId, correlationId);
        return await DiagnoseAsync(true, cancellationToken: ct);
    }

    private static string Reason(int count, AdminRow? u, AuthenticationUser? selected, bool format, bool match, AuthenticationAccess access)
    {
        if (u is null) return "LOGIN_NOT_FOUND"; if (count > 1) return "DUPLICATE_LOGIN";
        if (selected is null || selected.Id != u.Id) return "AUTH_REPOSITORY_SELECTED_DIFFERENT_USER";
        if (!u.Ativo) return "USER_INACTIVE"; if (u.Bloqueado) return "USER_BLOCKED"; if (u.IsDeleted) return "USER_DELETED";
        if (!u.TenantAtivo) return "TENANT_INACTIVE"; if (u.TenantIsDeleted) return "TENANT_DELETED";
        if (string.IsNullOrWhiteSpace(u.PasswordHash)) return "PASSWORD_HASH_MISSING"; if (!format) return "PASSWORD_HASH_INVALID_FORMAT";
        if (!match) return "PASSWORD_MISMATCH"; if (!u.HasGroup) return "NO_GROUP"; if (!u.HasProfile || access.Roles.Count == 0) return "NO_PROFILE";
        if (access.Permissions.Count == 0) return "NO_PERMISSIONS"; if (!u.HasEntity) return "NO_ENTITY"; if (!u.HasExercise) return "NO_EXERCISE"; return "OK";
    }
    private static bool IsValidHash(string? value) { var p = value?.Split('$'); return p is { Length: 4 } && p[0] == "SIGOV_PBKDF2_V1" && int.TryParse(p[1], out var n) && n is >= 100000 and <= 1000000; }
    private static string HashPrefix(string? value) => string.IsNullOrEmpty(value) ? "" : value[..Math.Min(value.Length, 18)] + "…";
    private async Task WriteReportAsync(DevelopmentAuthReport report, CancellationToken ct)
    {
        var path = Path.Combine(_environment.ContentRootPath, "..", "..", "artifacts", "local-setup", "dev-auth-report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), ct);
    }
    private sealed class AdminRow { public long Id { get; set; } public long? TenantId { get; set; } public string Login { get; set; } = ""; public string Email { get; set; } = ""; public string Nome { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string TipoUsuario { get; set; } = ""; public bool Ativo { get; set; } public bool Bloqueado { get; set; } public bool IsDeleted { get; set; } public bool DeveAlterarSenha { get; set; } public bool TenantAtivo { get; set; } public bool TenantIsDeleted { get; set; } public bool HasGroup { get; set; } public bool HasProfile { get; set; } public bool HasEntity { get; set; } public bool HasExercise { get; set; } }
}
