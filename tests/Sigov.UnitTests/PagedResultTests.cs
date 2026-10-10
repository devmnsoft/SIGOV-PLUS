using FluentAssertions;
using Sigov.Application.Common;
using Sigov.Application.Educacao.Avancada;
using Xunit;

namespace Sigov.UnitTests;

public sealed class PagedResultTests
{
    [Fact]
    public void Deve_Calcular_Total_De_Paginas_E_Navegacao()
    {
        var result = new PagedResult<int>(new[] { 1, 2 }, 2, 2, 5);

        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void Deve_Manter_Total_Filtrado_Em_Todas_As_Paginas()
    {
        var primeira = new PagedResult<int>(Enumerable.Range(1, 10).ToArray(), 1, 10, 23);
        var intermediaria = new PagedResult<int>(Enumerable.Range(11, 10).ToArray(), 2, 10, 23);
        var ultima = new PagedResult<int>(Enumerable.Range(21, 3).ToArray(), 3, 10, 23);

        primeira.TotalItems.Should().Be(23);
        intermediaria.TotalItems.Should().Be(23);
        ultima.TotalItems.Should().Be(23);
        new[] { primeira.Items.Count, intermediaria.Items.Count, ultima.Items.Count }.Should().Equal(10, 10, 3);
        ultima.TotalPages.Should().Be(3);
        ultima.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Deve_Tratar_Resultados_Vazio_E_Pagina_Fora_Do_Intervalo()
    {
        var vazio = PagedResult<int>.Empty(1, 10);
        var foraDoIntervalo = new PagedResult<int>(Array.Empty<int>(), 4, 10, 23);

        vazio.TotalItems.Should().Be(0);
        vazio.TotalPages.Should().Be(0);
        foraDoIntervalo.TotalItems.Should().Be(23);
        foraDoIntervalo.Items.Should().BeEmpty();
        foraDoIntervalo.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void Diagnostic_Padrao_Eh_Nulo_Quando_Nao_Ha_Diagnostico()
    {
        var normal = new PagedResult<int>(new[] { 1 }, 1, 10, 1);
        var vazia = PagedResult<int>.Empty(1, 10);

        normal.Diagnostic.Should().BeNull();
        vazia.Diagnostic.Should().BeNull();
    }

    [Fact]
    public void Indisponivel_Zera_Registros_E_Carrega_Diagnostico_Operacional()
    {
        var r = PagedResult<int>.Indisponivel(1, 10, "Recurso indisponível (schema ausente)");

        r.Items.Should().BeEmpty();
        r.TotalItems.Should().Be(0);
        r.Diagnostic.Should().Be("Recurso indisponível (schema ausente)");
    }

    [Fact]
    public void Csv_Campo_Nulo_Ou_Vazio_Retorna_Vazio()
    {
        EducacaoCsv.Campo(null).Should().Be(string.Empty);
        EducacaoCsv.Campo(string.Empty).Should().Be(string.Empty);
    }

    [Fact]
    public void Csv_Campo_Preserva_Texto_Simples_Sem_Mascarar()
    {
        EducacaoCsv.Campo("Escola A").Should().Be("Escola A");
        EducacaoCsv.Campo("12345").Should().Be("12345");
    }

    [Fact]
    public void Csv_Campo_Protege_Injecao_De_Formula_Em_Plilha()
    {
        EducacaoCsv.Campo("=SUM(A1)").Should().Be("'=SUM(A1)");
        EducacaoCsv.Campo("+cmd").Should().Be("'+cmd");
        EducacaoCsv.Campo("-x").Should().Be("'-x");
        EducacaoCsv.Campo("@ref").Should().Be("'@ref");
    }

    [Fact]
    public void Csv_Campo_Escapa_Asas_Delimitador_E_Quebra_De_Linha_Preservando_Tecido()
    {
        EducacaoCsv.Campo("a;b").Should().Be("\"a;b\"");
        EducacaoCsv.Campo("a\"b").Should().Be("\"a\"\"b\"");
        EducacaoCsv.Campo("linha1\nlinha2").Should().Be("\"linha1\nlinha2\"");
    }

    [Fact]
    public void Csv_Campo_Formulacao_E_Escape_Juntos()
    {
        EducacaoCsv.Campo("=A1;B2").Should().Be("\"'=A1;B2\"");
    }

    [Fact]
    public void Maquina_Status_Idempotencia_Para_MesmoStatus_Eh_Permitida()
    {
        EducacaoStatusMaquina.TransicaoPermitida("ATIVA", "ATIVA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("ENCERRADA", " encerrada ").Should().BeTrue();
    }

    [Fact]
    public void Maquina_Pendente_Pode_Ativar_Manter_Pendente_Ou_Cancelar()
    {
        EducacaoStatusMaquina.TransicaoPermitida("RASCUNHO", "ATIVA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("PROGRAMADA", "SOLICITADA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("RASCUNHO", "CANCELADA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("RASCUNHO", "ENCERRADA").Should().BeFalse();
        EducacaoStatusMaquina.TransicaoPermitida("RASCUNHO", "ATRASADO").Should().BeFalse();
    }

    [Fact]
    public void Maquina_Ativo_E_Alerta_Podem_Profundizar_Ou_Finalizar()
    {
        EducacaoStatusMaquina.TransicaoPermitida("ATIVA", "ATRASADO").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("ATIVA", "ENCERRADA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("SUSPENSA", "ATIVA").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("COM_INCONSISTENCIA", "ENCERRADO").Should().BeTrue();
        EducacaoStatusMaquina.TransicaoPermitida("ATRASADO", "PUBLICADO").Should().BeTrue();
    }

    [Fact]
    public void Maquina_Terminal_Eh_Final_Somente_Noop_Idempotente()
    {
        EducacaoStatusMaquina.TransicaoPermitida("ENCERRADA", "ATIVA").Should().BeFalse();
        EducacaoStatusMaquina.TransicaoPermitida("CANCELADO", "RASCUNHO").Should().BeFalse();
        EducacaoStatusMaquina.TransicaoPermitida("CANCELADA", "CANCELADA").Should().BeTrue();
    }

    [Fact]
    public void Maquina_Origem_Desconhecida_Preserva_Comportamento_Legado()
    {
        EducacaoStatusMaquina.Grupo("SINCRONIZADO").Should().BeNull();
        EducacaoStatusMaquina.TransicaoPermitida("SINCRONIZADO", "AGUARDANDO_SINCRONIZACAO").Should().BeTrue();
    }

    [Fact]
    public void Maquina_Destino_Vazio_Lanca_ArgumentException()
    {
        FluentActions.Invoking(() => EducacaoStatusMaquina.TransicaoPermitida("ATIVA", " "))
            .Should().Throw<ArgumentException>();
    }
}
