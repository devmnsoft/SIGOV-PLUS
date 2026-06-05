using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Abstractions;
using Sigov.Infrastructure.Common;
using Sigov.Infrastructure.Persistence.Dapper;
using Sigov.Infrastructure.Persistence.Migrations;
using Sigov.Infrastructure.Persistence.Repositories;
using Sigov.Infrastructure.Persistence.UnitOfWork;
using Sigov.Infrastructure.Security;
using Sigov.Application.Saas;
using Sigov.Infrastructure.Saas;
using Sigov.Application.Storage;
using Sigov.Infrastructure.Storage;
using Sigov.Application.Processos;
using Sigov.Infrastructure.Processos;
using Sigov.Application.Financeiro;
using Sigov.Infrastructure.Financeiro;

namespace Sigov.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<NpgsqlConnectionFactory>();
        services.AddScoped<DapperContext>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<MigrationRunner>();
        services.AddScoped<PessoaRepository>();
        services.AddScoped<UsuarioRepository>();
        services.AddScoped<AuditRepository>();
        services.AddScoped<IAuditService, ProcessosAuditService>();
        services.AddScoped<ILgpdMaskingService, LgpdMaskingService>();
        services.AddScoped<IDateTimeProvider, DateTimeProvider>();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddScoped<ICurrentExercicio, CurrentExercicio>();
        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantResolver, TenantResolver>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IFeatureFlagService, FeatureFlagService>();
        services.AddScoped<IModuloLicenciamentoService, ModuloLicenciamentoService>();
        services.AddScoped<ITenantUsageMeter, TenantUsageMeter>();
        services.AddScoped<ITenantConfigurationProvider, TenantConfigurationProvider>();
        services.AddScoped<Func<long, CancellationToken, Task<string?>>>(provider => async (tenantId, cancellationToken) =>
        {
            var context = provider.GetRequiredService<DapperContext>();
            using var connection = context.CreateConnection();
            return await Dapper.SqlMapper.ExecuteScalarAsync<string?>(connection, new Dapper.CommandDefinition("select status from sigov.tenant where id = @TenantId and ativo = true and is_deleted = false;", new { TenantId = tenantId }, cancellationToken: cancellationToken)).ConfigureAwait(false);
        });
        services.AddScoped<ITenantAccessGuard, TenantAccessGuard>();
        services.AddScoped<IFileHashService, FileHashService>();
        services.AddScoped<IStorageKeyGenerator, StorageKeyGenerator>();
        services.AddScoped<IFileTypeValidator, FileTypeValidator>();
        services.AddScoped<IAntivirusScanner, NoOpAntivirusScanner>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<ITipoProcessoRepository, TipoProcessoRepository>();
        services.AddScoped<IProcessoDigitalRepository, ProcessoDigitalRepository>();
        services.AddScoped<IProcessoMovimentacaoRepository, ProcessoMovimentacaoRepository>();
        services.AddScoped<IProcessoParecerRepository, ProcessoParecerRepository>();
        services.AddScoped<IProtocoloAtendimentoRepository, ProtocoloAtendimentoRepository>();
        services.AddScoped<IOuvidoriaRepository, OuvidoriaRepository>();
        services.AddScoped<IDiarioOficialRepository, DiarioOficialRepository>();
        services.AddScoped<IProcessoSequencialService, ProcessoSequencialRepository>();
        services.AddScoped<ITipoProcessoService, TipoProcessoService>();
        services.AddScoped<IProcessoDigitalService, ProcessoDigitalService>();
        services.AddScoped<IProtocoloAtendimentoService, ProtocoloAtendimentoService>();
        services.AddScoped<IOuvidoriaService, OuvidoriaService>();
        services.AddScoped<IDiarioOficialService, DiarioOficialService>();
        services.AddScoped<IPlanoContasRepository, PlanoContasRepository>();
        services.AddScoped<IFonteRecursoRepository, FonteRecursoRepository>();
        services.AddScoped<IProgramaRepository, ProgramaRepository>();
        services.AddScoped<IAcaoRepository, AcaoRepository>();
        services.AddScoped<INaturezaReceitaRepository, NaturezaReceitaRepository>();
        services.AddScoped<INaturezaDespesaRepository, NaturezaDespesaRepository>();
        services.AddScoped<IOrcamentoRepository, OrcamentoRepository>();
        services.AddScoped<IEmpenhoRepository, EmpenhoRepository>();
        services.AddScoped<ILiquidacaoRepository, LiquidacaoRepository>();
        services.AddScoped<IPagamentoRepository, PagamentoRepository>();
        services.AddScoped<IReceitaRepository, ReceitaRepository>();
        services.AddScoped<IFinanceiroSequencialService, FinanceiroSequencialRepository>();
        services.AddScoped<IFinanceiroDashboardRepository, FinanceiroDashboardRepository>();
        services.AddScoped<IPlanoContasService, PlanoContasService>();
        services.AddScoped<IFonteRecursoService, FonteRecursoService>();
        services.AddScoped<IProgramaService, ProgramaService>();
        services.AddScoped<IAcaoService, AcaoService>();
        services.AddScoped<INaturezaReceitaService, NaturezaReceitaService>();
        services.AddScoped<INaturezaDespesaService, NaturezaDespesaService>();
        services.AddScoped<IOrcamentoService, OrcamentoService>();
        services.AddScoped<IEmpenhoService, EmpenhoService>();
        services.AddScoped<ILiquidacaoService, LiquidacaoService>();
        services.AddScoped<IPagamentoService, PagamentoService>();
        services.AddScoped<IReceitaService, ReceitaService>();
        services.AddScoped<IFinanceiroDashboardService, FinanceiroDashboardService>();
        services.AddScoped<IFinanceiroExportacaoService, FinanceiroExportacaoService>();
        return services;
    }
}
