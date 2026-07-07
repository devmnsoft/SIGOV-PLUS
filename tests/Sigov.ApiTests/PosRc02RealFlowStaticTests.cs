using Xunit;

namespace Sigov.ApiTests;

public sealed class PosRc02RealFlowStaticTests
{
    private static readonly string ProtocolosApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/V1/ProtocolosApiController.cs"));
    private static readonly string DocumentosApi = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Controllers/V1/DocumentosApiController.cs"));
    private static readonly string Middleware = File.ReadAllText(TestRepoPath.Get("src/Sigov.Api/Middlewares/ApiKeyV1Middleware.cs"));
    private static readonly string OutboxSql = File.ReadAllText(TestRepoPath.Get("src/Sigov.Infrastructure/Outbox/OutboxSqlQueries.cs"));
    private static readonly string Smoke = File.ReadAllText(TestRepoPath.Get("scripts/smoke-test-sigov.ps1"));

    [Fact]
    public void ApiV1_Deve_Exigir_Key_Tenant_E_Escopo_Com_Log_Sem_Token_Claro()
    {
        Assert.Contains("X-Api-Key", Middleware);
        Assert.Contains("X-Tenant-Id", Middleware);
        Assert.Contains("protocolos.write", Middleware);
        Assert.Contains("documentos.write", Middleware);
        Assert.Contains("api_requisicao_log", Middleware);
        Assert.DoesNotContain("apiKey)", Middleware);
    }

    [Fact]
    public void Protocolo_Deve_Persistir_Workflow_Tarefa_Notificacao_E_Outbox()
    {
        Assert.Contains("insert into sigov.protocolo", ProtocolosApi);
        Assert.Contains("insert into sigov.workflow_instancia", ProtocolosApi);
        Assert.Contains("insert into sigov.tarefa", ProtocolosApi);
        Assert.Contains("insert into sigov.notificacao", ProtocolosApi);
        Assert.Contains("protocolo.criado", ProtocolosApi);
        Assert.Contains("protocolo.tramitado", ProtocolosApi);
    }

    [Fact]
    public void Ged_Deve_Persistir_Hash_Versao_Validacao_Publica_E_Outbox()
    {
        Assert.Contains("SHA256.HashData", DocumentosApi);
        Assert.Contains("insert into sigov.documento", DocumentosApi);
        Assert.Contains("insert into sigov.documento_versao", DocumentosApi);
        Assert.Contains("portal_validacao_documento", DocumentosApi);
        Assert.Contains("documento.criado", DocumentosApi);
    }

    [Fact]
    public void Outbox_Deve_Processar_Tabela_PosRc_E_Registrar_WebhookEntrega()
    {
        Assert.Contains("sigov.outbox_evento", OutboxSql);
        Assert.Contains("PROCESSANDO", OutboxSql);
        Assert.Contains("ENTREGUE", OutboxSql);
        Assert.Contains("FALHOU", OutboxSql);
        Assert.Contains("sigov.webhook_entrega", OutboxSql);
    }

    [Fact]
    public void Smoke_Deve_Cobrir_Rotas_Web_E_Api_V1_Reais()
    {
        Assert.Contains("/Protocolo/Novo", Smoke);
        Assert.Contains("/Ged/NovoDocumento", Smoke);
        Assert.Contains("/api/v1/protocolos sem key", Smoke);
        Assert.Contains("/api/v1/documentos com escopo válido", Smoke);
        Assert.Contains("/api/v1/tarefas com escopo válido", Smoke);
    }
}
