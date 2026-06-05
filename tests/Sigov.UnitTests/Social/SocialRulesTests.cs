using FluentAssertions;
using Sigov.Domain.Social;
using Xunit;

namespace Sigov.UnitTests.Social;
public sealed class SocialRulesTests
{
    [Fact] public void Familia_Exige_Codigo()=>FluentActions.Invoking(()=>new SocialFamilia(1,1," ")).Should().Throw<ArgumentException>();
    [Fact] public void Pessoa_Social_Exige_Pessoa()=>FluentActions.Invoking(()=>new SocialPessoa(1,1,0)).Should().Throw<ArgumentException>();
    [Fact] public void Composicao_Exige_Familia_E_Pessoa()=>FluentActions.Invoking(()=>new SocialComposicaoFamiliar(1,1,0,0,"FILHO")).Should().Throw<ArgumentException>();
    [Fact] public void Familia_Nao_Aceita_Dois_Responsaveis_Ativos(){var f=new SocialFamilia(1,1,"FAM-1"); f.AdicionarComposicao(new SocialComposicaoFamiliar(1,1,1,1,"RESP",true)); FluentActions.Invoking(()=>f.AdicionarComposicao(new SocialComposicaoFamiliar(1,1,1,2,"CONJUGE",true))).Should().Throw<InvalidOperationException>();}
    [Fact] public void Vulnerabilidade_Exige_Familia_Ou_Pessoa()=>FluentActions.Invoking(()=>new SocialVulnerabilidade(1,1,null,null,SocialVulnerabilidadeTipo.RENDA)).Should().Throw<ArgumentException>();
    [Fact] public void Concessao_Exige_Familia_Ou_Pessoa()=>FluentActions.Invoking(()=>new SocialBeneficioConcessao(1,1,1,null,null,SocialBeneficioStatus.SOLICITADO)).Should().Throw<ArgumentException>();
    [Fact] public void Concessao_Concedida_Exige_Autorizacao()=>FluentActions.Invoking(()=>new SocialBeneficioConcessao(1,1,1,1,null,SocialBeneficioStatus.CONCEDIDO)).Should().Throw<ArgumentException>();
    [Fact] public void Atendimento_Exige_Demanda()=>FluentActions.Invoking(()=>new SocialAtendimento(1,1,"ATSOC-1"," ")).Should().Throw<ArgumentException>();
    [Fact] public void Encaminhamento_Exige_Destino()=>FluentActions.Invoking(()=>new SocialEncaminhamento(1,1," ","Descrição")).Should().Throw<ArgumentException>();
    [Fact] public void Visita_Exige_Relato()=>FluentActions.Invoking(()=>new SocialVisita(1,1," ")).Should().Throw<ArgumentException>();
    [Fact] public void Coordenadas_Invalidas_Falham()=>FluentActions.Invoking(()=>new SocialVisita(1,1,"Relato",100,10)).Should().Throw<ArgumentException>();
    [Fact] public void Parecer_Exige_Texto()=>FluentActions.Invoking(()=>new SocialParecer(1,1,"Título"," ")).Should().Throw<ArgumentException>();
    [Fact] public void Indicador_Nao_Aceita_Valor_Negativo()=>FluentActions.Invoking(()=>new SocialVigilanciaIndicador(1,1,"IND","Indicador",-1)).Should().Throw<ArgumentException>();
}
