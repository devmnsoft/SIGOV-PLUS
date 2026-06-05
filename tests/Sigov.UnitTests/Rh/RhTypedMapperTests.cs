using FluentAssertions;
using Sigov.Application.Rh;
using Sigov.Application.Rh.Dto;
using Xunit;

namespace Sigov.UnitTests.Rh;

public sealed class RhTypedMapperTests
{
    [Fact]
    public void Mapper_Converte_ServidorCreateRequest_Para_RhRegistroCreateRequest()
    {
        var request = new ServidorCreateRequest("MAT-001", "Maria Silva", "12345678901", new DateOnly(1990, 1, 2), "maria@sigov.local", "maria@org.local", "11999990000");
        var mapped = RhTypedMapper.ToCreate(request);
        mapped.Dados.Should().ContainKey("matricula").WhoseValue.Should().Be("MAT-001");
        mapped.Dados.Should().ContainKey("nome").WhoseValue.Should().Be("Maria Silva");
        mapped.Dados.Should().ContainKey("cpf").WhoseValue.Should().Be("12345678901");
        mapped.Dados.Should().ContainKey("classificacaoLgpd").WhoseValue.Should().Be("dados_pessoais_sensiveis");
    }

    [Fact]
    public void Mapper_Mascara_Dados_Sensiveis_Em_ServidorResponse()
    {
        var response = new RhRegistroResponse(10, "servidores", new Dictionary<string, object?>
        {
            ["matricula"] = "MAT-001",
            ["nome"] = "Maria Silva",
            ["cpf"] = "12345678901",
            ["emailInstitucional"] = "maria@org.local",
            ["telefone"] = "11999990000",
            ["banco"] = "001"
        }, true, DateTimeOffset.UtcNow, null);

        var mapped = RhTypedMapper.ToServidor(response);
        mapped.Cpf.Should().Be("123******01");
        mapped.EmailInstitucional.Should().Be("m***@org.local");
        mapped.Telefone.Should().Be("***0000");
        mapped.ClassificacaoLgpd.Should().Be("dados_pessoais_sensiveis");
    }
}
