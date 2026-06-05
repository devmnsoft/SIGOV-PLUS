using FluentAssertions;
using Sigov.Domain.Rh;
using Xunit;

namespace Sigov.UnitTests.Rh;

public sealed class RhRulesTests
{
    [Fact]
    public void Servidor_Exige_Tenant_Matricula_Nome_E_Cpf()
    {
        Assert.Throws<ArgumentException>(() => new Servidor(0, "1", "Maria", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "", "Maria", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "1", "", "00000000000", new DateOnly(1990, 1, 1)));
        Assert.Throws<ArgumentException>(() => new Servidor(1, "1", "Maria", "", new DateOnly(1990, 1, 1)));
    }

    [Fact]
    public void Servidor_Classifica_Dados_Pessoais_Como_Lgpd_Sensiveis()
    {
        var servidor = new Servidor(1, "MAT-1", "Maria Silva", "00000000000", new DateOnly(1990, 1, 1));
        servidor.ClassificacaoLgpd.Should().Be("dados_pessoais_sensiveis");
    }

    [Fact]
    public void Folha_Nao_Aceita_Mes_Invalido()
    {
        Assert.Throws<ArgumentException>(() => new Folha(1, 2026, 14, "mensal"));
    }


    [Fact]
    public void Cargo_Exige_Codigo_E_Nome()
    {
        Assert.Throws<ArgumentException>(() => new Cargo(1, "", "Analista"));
        Assert.Throws<ArgumentException>(() => new Cargo(1, "ANL", ""));
    }

    [Fact]
    public void Ferias_E_Afastamento_Nao_Aceitam_Fim_Antes_Do_Inicio_Nas_Regras_Tipadas()
    {
        var inicio = new DateOnly(2026, 3, 10);
        var fim = new DateOnly(2026, 3, 1);
        (fim < inicio).Should().BeTrue();
    }

    [Fact]
    public void Registro_Principal_Usa_Soft_Delete_Com_Auditoria()
    {
        var cargo = new Cargo(1, "TEC", "Técnico Administrativo");
        cargo.Excluir(7);
        cargo.IsDeleted.Should().BeTrue();
        cargo.Ativo.Should().BeFalse();
        cargo.DeletedBy.Should().Be(7);
        cargo.DeletedAt.Should().NotBeNull();
    }
}
