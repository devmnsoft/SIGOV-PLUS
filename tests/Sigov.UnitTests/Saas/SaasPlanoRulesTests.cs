using FluentAssertions;
using Sigov.Application.Saas.Comercial;
using Sigov.Domain.Saas.Comercial;
using Xunit;

namespace Sigov.UnitTests.Saas;

public sealed class SaasPlanoRulesTests
{
    [Fact] public void Plano_exige_codigo_e_nome() => new SaasPlano(0, "", "", "descrição", true, SaasPlanoTipo.Publico, 0, SaasPeriodicidade.Mensal, 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Preco_negativo_falha() => new SaasPlano(0, "ESS", "Essencial", "descrição", true, SaasPlanoTipo.Publico, -1, SaasPeriodicidade.Mensal, 1, false, false).Validate().IsFailure.Should().BeTrue();
    [Fact] public void Limite_de_usuarios_negativo_falha() => new SaasPlano(0, "ESS", "Essencial", "descrição", true, SaasPlanoTipo.Publico, 1, SaasPeriodicidade.Mensal, -1, false, false).Validate().IsFailure.Should().BeTrue();

    [Fact] public void Diff_de_plano_identico_nao_altera_modulos()
    {
        var (adicionados, removidos) = PlanModuleDiff.Calcular(new[] { "protocolo", "core" }, new[] { "CORE", "Protocolo" });
        adicionados.Should().BeEmpty();
        removidos.Should().BeEmpty();
    }

    [Fact] public void Diff_do_upgrade_somente_adiciona_modulos_novos()
    {
        var (adicionados, removidos) = PlanModuleDiff.Calcular(new[] { "protocolo" }, new[] { "protocolo", "compras", "financeiro" });
        adicionados.OrderBy(x => x).Should().Equal("compras", "financeiro");
        removidos.Should().BeEmpty();
    }

    [Fact] public void Diff_do_downgrade_somente_remove_modulos_que_saiu_do_plano()
    {
        var (adicionados, removidos) = PlanModuleDiff.Calcular(new[] { "protocolo", "compras" }, new[] { "protocolo" });
        adicionados.Should().BeEmpty();
        removidos.Should().ContainSingle().Which.Should().Be("compras");
    }

    [Fact] public void Diff_ignora_diferencas_de_caixa_em_codigos_de_modulo()
    {
        var (adicionados, removidos) = PlanModuleDiff.Calcular(new[] { "COMPRAS_EMPRESARIAIS" }, new[] { "compras_empresariais" });
        adicionados.Should().BeEmpty();
        removidos.Should().BeEmpty();
    }

    [Fact] public void Diff_ordena_resultados_para_auditoria_estavel()
    {
        var (adicionados, _) = PlanModuleDiff.Calcular(Array.Empty<string>(), new[] { "zeta", "alfa" });
        adicionados.Should().ContainInOrder("alfa", "zeta");
    }
}
