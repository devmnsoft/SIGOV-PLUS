using FluentAssertions;
using Sigov.Domain.Saneamento;
using Xunit;

namespace Sigov.UnitTests.Saneamento;

public sealed class SaneamentoRulesTests
{
    [Fact] public void Consumidor_Exige_Pessoa() => FluentActions.Invoking(() => new SaneamentoConsumidor(1, 1, 0, "CON-1")).Should().Throw<ArgumentException>();
    [Fact] public void Unidade_Exige_Consumidor() => FluentActions.Invoking(() => new UnidadeConsumidora(1, 1, 0, "UC-1")).Should().Throw<ArgumentException>();
    [Fact] public void Hidrometro_Exige_Numero_Serie() => FluentActions.Invoking(() => new Hidrometro(1, 1, 1, " ")).Should().Throw<ArgumentException>();
    [Fact] public void Leitura_Menor_Que_Anterior_Falha_Sem_Ajuste() => FluentActions.Invoking(() => new LeituraConsumo(1, 1, 1, 20, 10, 0, TipoLeitura.Normal)).Should().Throw<ArgumentException>();
    [Fact] public void Leitura_Calcula_Consumo_Medido() { var leitura = new LeituraConsumo(1, 1, 1, 10, 25, 15, TipoLeitura.Normal); leitura.ConsumoMedido.Should().Be(15); }
    [Fact] public void Fatura_Paga_Nao_Recebe_Novo_Pagamento() { var fatura = new FaturaSaneamento(1, 1, 1, "FAT-1", 10); fatura.RegistrarPagamento(10); FluentActions.Invoking(() => fatura.RegistrarPagamento(1)).Should().Throw<InvalidOperationException>(); }
    [Fact] public void Fatura_Cancelada_Nao_Recebe_Pagamento() { var fatura = new FaturaSaneamento(1, 1, 1, "FAT-1", 10); fatura.Cancelar(); FluentActions.Invoking(() => fatura.RegistrarPagamento(1)).Should().Throw<InvalidOperationException>(); }
    [Fact] public void Pagamento_Nao_Ultrapassa_Saldo() { var fatura = new FaturaSaneamento(1, 1, 1, "FAT-1", 10); FluentActions.Invoking(() => fatura.RegistrarPagamento(11)).Should().Throw<InvalidOperationException>(); }
    [Fact] public void Parcelamento_Exige_Parcelas_Maior_Que_Zero() => FluentActions.Invoking(() => new ParcelamentoSaneamento(1, 0, 100)).Should().Throw<ArgumentException>();
    [Fact] public void Ordem_Cancelada_Nao_Executa() { var ordem = new OrdemServicoSaneamento(1, 1, "OS-1", "CORTE", "Descrição"); ordem.Cancelar(); FluentActions.Invoking(() => ordem.Executar("Solução")).Should().Throw<InvalidOperationException>(); }
    [Fact] public void Amostra_Exige_Ponto_Coleta() => FluentActions.Invoking(() => new LaboratorioAmostra("AMO-1", " ", DateTimeOffset.UtcNow)).Should().Throw<ArgumentException>();
    [Fact] public void Coordenadas_Invalidas_Falham() => FluentActions.Invoking(() => new UnidadeConsumidora(1, 1, 1, "UC-1", 100, -50)).Should().Throw<ArgumentException>();
}
