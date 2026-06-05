using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;

public sealed class RhModuleSmokeTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Rh_Migration_Deve_Criar_Tabelas_No_Schema_Sigov_Com_Tenant_E_Soft_Delete()
    {
        var sql = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/020_rh_completo.sql")).ToLowerInvariant();
        foreach (var table in new[] { "servidor", "cargo", "lotacao", "vinculo", "folha", "folha_evento", "folha_lancamento", "ponto", "ferias", "afastamento", "saude_ocupacional", "esocial", "portal_usuario", "portal_acesso", "rh_evento" })
        {
            sql.Should().Contain("'" + table + "'");
        }

        sql.Should().Contain("create table if not exists sigov.%i");
        sql.Should().Contain("tenant_id bigint not null references sigov.tenant(id)");
        sql.Should().Contain("is_deleted boolean not null default false");
        sql.Should().Contain("dados jsonb not null default '{}'::jsonb");
        sql.Should().NotContain("create schema rh");
    }

    [Fact]
    public void Rh_Repository_Deve_Usar_Dapper_Parametrizado_Filtrar_Tenant_E_Auditar()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Rh/RhRepository.cs"));
        code.Should().Contain("TenantId = tenantId");
        code.Should().Contain("where tenant_id = @TenantId");
        code.Should().Contain("cast(@Dados as jsonb)");
        code.Should().Contain("jsonb_build_object('operacao','CRIAR'");
        code.Should().Contain("RegistrarEventoAsync");
        code.Should().Contain("sigov.rh_evento");
    }

    [Fact]
    public void Rh_Service_Deve_Validar_Payloads_Criticos_No_Backend()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhServices.cs"));
        code.Should().Contain("CamposObrigatorios");
        code.Should().Contain("CPF deve conter 11 dígitos");
        code.Should().Contain("Mês da folha deve estar entre 1 e 13");
        code.Should().Contain("Valor do lançamento não pode ser negativo");
        code.Should().Contain("Formato de exportação inválido");
        code.Should().Contain("Ações de RH bloqueadas em exercício encerrado");
        code.Should().Contain("ExercicioAbertoAsync");
    }

    [Fact]
    public void Rh_Deve_Mascarar_Dados_Pessoais_E_Sensiveis()
    {
        var repository = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Rh/RhRepository.cs"));
        var policy = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhLgpdMaskingPolicy.cs"));
        repository.Should().Contain("MaskDadosPessoais");
        repository.Should().Contain("MaskEmail");
        repository.Should().Contain("MaskTelefone");
        repository.Should().Contain("classificacaoLgpd");
        policy.Should().Contain("resultadoExame");
        policy.Should().Contain("motivoSensivel");
        policy.Should().Contain("dados_pessoais_sensiveis");
    }

    [Fact]
    public void Rh_Api_Deve_Preservar_Endpoints_Genericos_E_Adicionar_Tipados()
    {
        var generic = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/RhController.cs"));
        var typed = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/RhTypedController.cs"));
        generic.Should().Contain("api/rh");
        generic.Should().Contain("dashboard");
        generic.Should().Contain("portal/servidores");
        generic.Should().Contain("export/{recurso}.{formato}");
        generic.Should().Contain("integrar-financeiro");
        typed.Should().Contain("servidores-tipado");
        typed.Should().Contain("folhas-tipado/{folhaId:long}/lancamentos");
        typed.Should().Contain("portal-tipado/servidores");
    }

    [Fact]
    public void Rh_Typed_Service_Deve_Usar_Service_Generico_Como_Fachada()
    {
        var code = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhTypedService.cs"));
        code.Should().Contain("IRhService _service");
        code.Should().Contain("CriarServidorAsync");
        code.Should().Contain("CriarLancamentoFolhaAsync");
        code.Should().Contain("ObterPortalServidorAsync");
    }

    [Fact]
    public void Rh_Frontend_Deve_Ter_Antiforgery_Jquery_Ajax_E_Modulos()
    {
        var view = File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/Views/Rh/_Registro.cshtml"));
        var js = File.ReadAllText(Path.Combine(Root, "src/Sigov.Web/wwwroot/js/modules/rh.js"));
        view.Should().Contain("@Html.AntiForgeryToken()");
        view.Should().Contain("api/rh/export");
        js.Should().Contain("api.request");
        js.Should().Contain("RequestVerificationToken");
        js.Should().Contain("status === 401");
        js.Should().Contain("status === 403");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
