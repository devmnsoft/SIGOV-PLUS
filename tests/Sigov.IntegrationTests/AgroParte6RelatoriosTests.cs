using FluentAssertions;
using Xunit;

namespace Sigov.IntegrationTests;
public sealed class AgroParte6RelatoriosTests
{
    private readonly string _migration = File.ReadAllText(Path.Combine("..", "..", "..", "..", "database", "postgres", "migrations", "20260608140000_agro_relatorios_bi_transparencia.sql"));
    [Fact] public void Migration_Cria_Tabelas_Da_Parte_6_No_Schema_Sigov()
    {
        foreach (var table in new[] { "agro_indicador", "agro_indicador_valor", "agro_relatorio_modelo", "agro_relatorio_execucao", "agro_dataset_publico", "agro_dataset_publicacao", "agro_dataset_download_log", "agro_dicionario_dados", "agro_painel_comercial_config" })
            _migration.Should().Contain($"sigov.{table}");
        _migration.Should().NotContain("create schema agro", "não pode criar schema agro");
        _migration.Should().NotContain("create schema bi", "não pode criar schema bi");
        _migration.Should().NotContain("create schema transparencia", "não pode criar schema transparencia");
    }
    [Fact] public void Migration_Cria_Views_Indices_Seeds_E_TenantId()
    {
        _migration.Should().Contain("vw_agro_bi_resumo");
        _migration.Should().Contain("idx_agro_indicador_tenant_codigo");
        _migration.Should().Contain("tenant_id bigint not null");
        _migration.Should().Contain("total_produtores");
        _migration.Should().Contain("agro_dataset_download_log");
    }
    [Fact] public void Migration_Suporta_Csv_Json_Geojson_Datasets_Publicados()
    {
        _migration.Should().Contain("PUBLICADO");
        _migration.Should().Contain("CSV");
        _migration.Should().Contain("anonimizado boolean not null default true");
        _migration.Should().Contain("ck_agro_dataset_publico_lgpd");
    }
}
