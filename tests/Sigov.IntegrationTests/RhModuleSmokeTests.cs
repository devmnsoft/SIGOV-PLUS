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
        code.Should().Contain("TotalLancamentosFolhaAsync");
        code.Should().Contain("Folha deve possuir lançamentos válidos");
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
        typed.Should().Contain("folhas-tipado/integrar-financeiro");
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

    [Fact]
    public void Rh_Portal_S8_Deve_Resolver_Vinculo_No_Servidor_E_Filtrar_Pelo_Proprio_Servidor()
    {
        var domain = File.ReadAllText(Path.Combine(Root, "src/Sigov.Domain/Rh/PortalRegras.cs"));
        domain.Should().Contain("VINCULO_PORTAL_NAO_ENCONTRADO");
        domain.Should().Contain("LANCAMENTO_FORA_DO_ESCOPO");
        domain.Should().Contain("Math.Abs(valor)");

        var repo = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Rh/RhRepository.cs"));
        repo.Should().Contain("sigov.rh_portal_usuario v");
        repo.Should().Contain("v.servidor_id as ServidorId");
        repo.Should().Contain("RecursosComColunaServidorId");
        repo.Should().Contain("'INTEGRACAO_FOLHA'::varchar");

        var service = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhServices.cs"));
        service.Should().Contain("ResolverVinculoPortalAsync");
        service.Should().Contain("PortalRegras.TotaisPorTipo");
        service.Should().Contain("PertenceAoServidor(LerServidorIdDoPayload");
        service.Should().Contain("\"portal-competencias\"");
        service.Should().Contain("PORTAL_SECAO");

        var controller = File.ReadAllText(Path.Combine(Root, "src/Sigov.Api/Controllers/RhBloco2Controller.cs"));
        controller.Should().Contain("PortalSecaoAsync(secao");
        controller.Should().Contain("ObterPortalLancamentoAsync(id,ct)");
        controller.Should().Contain("[HttpGet(\"portal/pendencias\")]");
    }

    [Fact]
    public void S9_Outbox_Deve_Usar_Schema_Real_Da_Fila_E_Registro_De_Retry()
    {
        var queries = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Outbox/OutboxSqlQueries.cs"));
        queries.Should().Contain("lower(status) in ('pendente','pending','erro')");
        queries.Should().Contain("next_attempt_at is null or next_attempt_at <= now()");
        queries.Should().Contain("f.tipo_evento as TipoEvento");
        queries.Should().Contain("processed_at=now()");
        queries.Should().Contain("left(@Erro, 500)");
        queries.Should().NotContain("is_deleted");
        queries.Should().NotContain("proxima_tentativa_at");
        queries.Should().NotContain("erro_mascarado=null");

        // O handler financeiro precisa ser o PRIMEIRO na cadeia (ordem de registro resolve via First).
        var program = File.ReadAllText(Path.Combine(Root, "src/Sigov.Worker/Program.cs"));
        var posFolha = program.IndexOf("FolhaPontoFinanceiraOutboxHandler", StringComparison.Ordinal);
        var posWebhook = program.IndexOf("WebhookOutboxHandler", StringComparison.Ordinal);
        posFolha.Should().BeGreaterThan(-1);
        posWebhook.Should().BeGreaterThan(posFolha);
    }

    [Fact]
    public void S9_Produzidor_Da_Fila_E_Consumidor_Do_Empenho_Deve_Compartilhar_Chave_Idempotencia()
    {
        var repo = File.ReadAllText(Path.Combine(Root, "src/Sigov.Infrastructure/Rh/RhRepository.cs"));
        repo.Should().Contain("insert into sigov.outbox_evento");
        repo.Should().Contain("on conflict (idempotency_key) where idempotency_key is not null do nothing");
        repo.Should().Contain("InserirFilaFinanceiraAsync(integracaoId, dtos, proventos, descontos)");
        repo.Should().Contain("ConsultarIntegracaoFinanceiraAsync");
        repo.Should().Contain("FolhaPontoFinanceiraRegras.ChaveIntegracao(tenantId, integracaoId)");

        var handler = File.ReadAllText(Path.Combine(Root, "src/Sigov.Worker/Outbox/Handlers/FolhaPontoFinanceiraOutboxHandler.cs"));
        handler.Should().Contain("string.Equals(tipoEvento, FolhaPontoFinanceiraRegras.TipoEvento, StringComparison.Ordinal)");
        handler.Should().Contain("FolhaPontoFinanceiraRegras.ChaveIntegracao(message.TenantId, payload.IntegracaoId)");
        handler.Should().Contain("FolhaPontoFinanceiraRegras.ConstruirEmpenho(payload, orcamento, fornecedor)");
        handler.Should().Contain("FalhaExercicioAusente");
        handler.Should().Contain("_empenhos.CriarAsync(message.TenantId, payload.EntidadeId, payload.ExercicioId, ano, request, payload.UsuarioId, chave, cancellationToken)");

        var contracts = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhContracts.cs"));
        contracts.Should().Contain("RhIntegracaoFolhaFinanceiraStatusDto(bool Habilitada, string? ChaveIdempotencia, long? EventoFilaId, string? StatusFila, int TentativasFila, string? ErroFila, DateTime? ProcessadaEm, long? DocumentoEmpenhoId)");
        contracts.Should().Contain("Task<RhIntegracaoFinanceiraConsulta> ConsultarIntegracaoFinanceiraAsync(long tenantId, long integracaoId, CancellationToken ct)");

        var services = File.ReadAllText(Path.Combine(Root, "src/Sigov.Application/Rh/RhServices.cs"));
        services.Should().Contain("FolhaPontoFinanceiraRegras.ParametroHabilitar");
        services.Should().Contain("_repo.ConsultarIntegracaoFinanceiraAsync(TenantId, integracao.IntegracaoId, ct)");
    }

    [Fact]
    public void S9_Migration_Parametros_Folha_Financeira_Deve_Ser_Idempotente_FailClosed_E_Nunca_Habilitar_Por_Padrao()
    {
        var sql = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/20261007120000_evo_rh_folha_financeira_parametros.sql"));
        sql.Should().Contain("'ORCAMENTO_DESPESA_FOLHA_ID'");
        sql.Should().Contain("'FORNECEDOR_FOLHA_ID'");
        sql.Should().Contain("on conflict (modulo,codigo) where is_deleted=false");
        sql.Should().Contain("'null'::jsonb");
        sql.Should().Contain("select id from sigov.tenant where id = 5 and ativo and not is_deleted loop");
        sql.Should().Contain("SEED_FICTICIO_DEV");
        // HABILITAR_INTEGRACAO_FINANCEIRA nunca é semeado true: a ativação é ato do administrador.
        sql.Should().NotContain("'HABILITAR_INTEGRACAO_FINANCEIRA'");

        var manifest = File.ReadAllText(Path.Combine(Root, "database/postgres/migrations/manifest.json"));
        manifest.Should().Contain("\"version\": \"20261007120000\"");
        manifest.Should().Contain("d9774e985ce427971d6374726018f7d128d3849664bb8d7377ce715b311065e4");

        foreach (var script in new[] { "script_completop.sql", "script_completo.sql", "script_completo_dev.sql", "database/script_completo.sql" })
        {
            var consolidado = File.ReadAllText(Path.Combine(Root, script));
            consolidado.Should().Contain("20261007120000_evo_rh_folha_financeira_parametros.sql");
            consolidado.Should().Contain("d9774e985ce427971d6374726018f7d128d3849664bb8d7377ce715b311065e4");
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "sigov.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Raiz do repositório sigov não encontrada.");
    }
}
