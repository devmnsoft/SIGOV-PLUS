using FluentAssertions;
using Sigov.Domain.Agro.Transparencia;
using Xunit;

namespace Sigov.UnitTests.Agro;
public sealed class AgroDatasetPublicoRulesTests
{
    [Fact] public void Dataset_Publico_Deve_Estar_Anonimizado() => FluentActions.Invoking(() => new AgroDatasetPublico(1, null, "prod", "Produção", AgroDatasetTipo.PRODUCAO_AGREGADA, false, true)).Should().Throw<ArgumentException>();
    [Fact] public void Dataset_Nao_Anonimizado_Nao_Pode_Ser_Publicado() => FluentActions.Invoking(() => new AgroDatasetPublico(1, null, "prod", "Produção", AgroDatasetTipo.PRODUCAO_AGREGADA, false, false).ValidarPublicacao()).Should().Throw<InvalidOperationException>();
    [Fact] public void Publicacao_Publica_Exige_Status_Publicado() => FluentActions.Invoking(() => new AgroDatasetPublicacao(1, 1, AgroDatasetStatus.RASCUNHO, "CSV").ValidarDownloadPublico()).Should().Throw<InvalidOperationException>();
    [Fact] public void Download_Publico_Registra_Log_Com_Dataset_Ou_Publicacao() => new AgroDatasetDownloadLog(1, 2, null, "CSV", "127.0.0.1").DatasetId.Should().Be(2);
}
