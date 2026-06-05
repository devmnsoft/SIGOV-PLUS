using System.ComponentModel.DataAnnotations;
namespace Sigov.Web.Models.Social;
public sealed class SocialDashboardViewModel { public string ApiBase { get; set; } = "/api/social"; }
public sealed class SocialUnidadeFormViewModel { [Required] public string Codigo { get; set; }=""; [Required] public string Nome { get; set; }=""; public string TipoUnidade { get; set; }="CRAS"; public string Situacao { get; set; }="ATIVA"; public decimal? Latitude { get; set; } public decimal? Longitude { get; set; } }
public sealed class SocialFamiliaFormViewModel { public string? CodigoFamilia { get; set; } public string? NisFamiliar { get; set; } public string Situacao { get; set; }="ATIVA"; public string? ClassificacaoRisco { get; set; } public decimal? RendaFamiliar { get; set; } }
public sealed class SocialPessoaFormViewModel { [Required] public long PessoaId { get; set; } public long? FamiliaId { get; set; } public string? Nis { get; set; } public string Situacao { get; set; }="ATIVA"; }
public sealed class SocialComposicaoFamiliarViewModel { [Required] public long SocialPessoaId { get; set; } [Required] public string Parentesco { get; set; }=""; public bool ResponsavelFamiliar { get; set; } }
public sealed class SocialVulnerabilidadeFormViewModel { public long? FamiliaId { get; set; } public long? SocialPessoaId { get; set; } [Required] public string TipoVulnerabilidade { get; set; }="RENDA"; public string Grau { get; set; }="MEDIO"; }
public sealed class SocialProgramaFormViewModel { [Required] public string Codigo { get; set; }=""; [Required] public string Nome { get; set; }=""; public string TipoPrograma { get; set; }="MUNICIPAL"; }
public sealed class SocialBeneficioFormViewModel { [Required] public string Codigo { get; set; }=""; [Required] public string Nome { get; set; }=""; public string TipoBeneficio { get; set; }="EVENTUAL"; public decimal? ValorReferencia { get; set; } }
public sealed class SocialBeneficioConcessaoFormViewModel { [Required] public long BeneficioId { get; set; } public long? FamiliaId { get; set; } public long? SocialPessoaId { get; set; } public decimal? Valor { get; set; } }
public sealed class SocialAtendimentoFormViewModel { public long? FamiliaId { get; set; } public long? SocialPessoaId { get; set; } [Required] public string Demanda { get; set; }=""; public string TipoAtendimento { get; set; }="ACOLHIDA"; }
public sealed class SocialEncaminhamentoFormViewModel { [Required] public string Destino { get; set; }=""; [Required] public string Descricao { get; set; }=""; }
public sealed class SocialVisitaFormViewModel { public long? FamiliaId { get; set; } [Required] public string Relato { get; set; }=""; public string Motivo { get; set; }="ACOMPANHAMENTO"; public decimal? Latitude { get; set; } public decimal? Longitude { get; set; } }
public sealed class SocialParecerFormViewModel { [Required] public string Titulo { get; set; }=""; [Required] public string Texto { get; set; }=""; public bool Sigiloso { get; set; }=true; }
public sealed class SocialAcompanhamentoFormViewModel { [Required] public long FamiliaId { get; set; } [Required] public string Objetivo { get; set; }=""; }
public sealed class SocialVigilanciaFormViewModel { public string Codigo { get; set; }=""; public string Nome { get; set; }=""; public decimal Valor { get; set; } }
