using Sigov.Domain.Common;

namespace Sigov.Domain.Social;

public enum SocialUnidadeTipo { CRAS, CREAS, CENTRO_POP, ABRIGO, SECRETARIA, OUTROS }
public enum SocialUnidadeSituacao { ATIVA, INATIVA }
public enum SocialFamiliaSituacao { ATIVA, INATIVA, ACOMPANHAMENTO }
public enum SocialClassificacaoRisco { BAIXO, MEDIO, ALTO, CRITICO }
public enum SocialPessoaSituacao { ATIVA, INATIVA }
public enum SocialCadastroStatus { ABERTO, ATUALIZADO, SUSPENSO, ENCERRADO }
public enum SocialVulnerabilidadeTipo { RENDA, MORADIA, ALIMENTAR, VIOLENCIA, SAUDE, DEFICIENCIA, IDOSO, CRIANCA_ADOLESCENTE, TRABALHO_INFANTIL, POP_RUA, OUTROS }
public enum SocialGrauVulnerabilidade { BAIXO, MEDIO, ALTO, CRITICO }
public enum SocialVulnerabilidadeStatus { ABERTA, EM_ACOMPANHAMENTO, RESOLVIDA, CANCELADA }
public enum SocialProgramaTipo { MUNICIPAL, ESTADUAL, FEDERAL, OUTROS }
public enum SocialBeneficioTipo { EVENTUAL, CONTINUADO, ALIMENTACAO, AUXILIO_NATALIDADE, AUXILIO_FUNERAL, ALUGUEL_SOCIAL, OUTROS }
public enum SocialBeneficioStatus { SOLICITADO, EM_ANALISE, CONCEDIDO, INDEFERIDO, CANCELADO, ENTREGUE }
public enum SocialAtendimentoTipo { ACOLHIDA, PAIF, PAEFI, BENEFICIO, ORIENTACAO, OUTROS }
public enum SocialAtendimentoStatus { ABERTO, EM_ACOMPANHAMENTO, CONCLUIDO, CANCELADO }
public enum SocialEncaminhamentoStatus { PENDENTE, ENVIADO, RETORNADO, CANCELADO }
public enum SocialVisitaStatus { AGENDADA, REALIZADA, NAO_REALIZADA, CANCELADA }
public enum SocialParecerTipo { SOCIAL, BENEFICIO, VISITA, VIGILANCIA, OUTROS }
public enum SocialAcompanhamentoStatus { ATIVO, ENCERRADO, SUSPENSO }
public enum SocialVigilanciaOcorrenciaStatus { ABERTA, EM_ANALISE, RESOLVIDA, CANCELADA }

public abstract class SocialEntity : AggregateRoot
{
    protected static string Required(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
    protected static void ValidarTenantEntidade(long tenantId, long entidadeId)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade obrigatória.", nameof(entidadeId));
    }
    protected static void ValidarCoordenadas(decimal? latitude, decimal? longitude)
    {
        if (latitude is < -90 or > 90) throw new ArgumentException("Latitude inválida.", nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentException("Longitude inválida.", nameof(longitude));
        if ((latitude.HasValue && !longitude.HasValue) || (!latitude.HasValue && longitude.HasValue)) throw new ArgumentException("Latitude e longitude devem ser informadas em conjunto.", nameof(longitude));
    }
}

public sealed class SocialUnidade : SocialEntity
{
    public SocialUnidade(long tenantId, long entidadeId, string codigo, string nome, SocialUnidadeTipo tipo, decimal? latitude = null, decimal? longitude = null)
    { ValidarTenantEntidade(tenantId, entidadeId); ValidarCoordenadas(latitude, longitude); TenantId=tenantId; EntidadeId=entidadeId; Codigo=Required(codigo,nameof(codigo)); Nome=Required(nome,nameof(nome)); TipoUnidade=tipo; Latitude=latitude; Longitude=longitude; }
    public long TenantId { get; } public long EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public SocialUnidadeTipo TipoUnidade { get; } public decimal? Latitude { get; } public decimal? Longitude { get; }
}
public sealed class SocialFamilia : SocialEntity
{
    private readonly List<SocialComposicaoFamiliar> _composicao = new();
    public SocialFamilia(long tenantId, long entidadeId, string codigoFamilia) { ValidarTenantEntidade(tenantId, entidadeId); TenantId=tenantId; EntidadeId=entidadeId; CodigoFamilia=Required(codigoFamilia,nameof(codigoFamilia)); }
    public long TenantId { get; } public long EntidadeId { get; } public string CodigoFamilia { get; } public IReadOnlyCollection<SocialComposicaoFamiliar> Composicao => _composicao;
    public void AdicionarComposicao(SocialComposicaoFamiliar composicao) { if (composicao.FamiliaId <= 0) throw new ArgumentException("Composição exige família.", nameof(composicao)); if (composicao.ResponsavelFamiliar && _composicao.Any(x => x.Ativo && x.ResponsavelFamiliar)) throw new InvalidOperationException("Família já possui responsável familiar ativo."); _composicao.Add(composicao); }
}
public sealed class SocialPessoa : SocialEntity
{ public SocialPessoa(long tenantId,long entidadeId,long pessoaId,long? familiaId=null){ValidarTenantEntidade(tenantId,entidadeId); TenantId=tenantId; EntidadeId=entidadeId; PessoaId=pessoaId>0?pessoaId:throw new ArgumentException("Pessoa social deve estar vinculada a uma pessoa.",nameof(pessoaId)); FamiliaId=familiaId;} public long TenantId{get;} public long EntidadeId{get;} public long PessoaId{get;} public long? FamiliaId{get;} }
public sealed class SocialComposicaoFamiliar : SocialEntity
{ public SocialComposicaoFamiliar(long tenantId,long entidadeId,long familiaId,long socialPessoaId,string parentesco,bool responsavelFamiliar=false){ValidarTenantEntidade(tenantId,entidadeId); TenantId=tenantId; EntidadeId=entidadeId; FamiliaId=familiaId>0?familiaId:throw new ArgumentException("Composição exige família.",nameof(familiaId)); SocialPessoaId=socialPessoaId>0?socialPessoaId:throw new ArgumentException("Composição exige pessoa social.",nameof(socialPessoaId)); Parentesco=Required(parentesco,nameof(parentesco)); ResponsavelFamiliar=responsavelFamiliar; Ativo=true;} public long TenantId{get;} public long EntidadeId{get;} public long FamiliaId{get;} public long SocialPessoaId{get;} public string Parentesco{get;} public bool ResponsavelFamiliar{get;} public bool Ativo{get;} }
public sealed class SocialCadastro : SocialEntity { public SocialCadastro(long tenantId,long entidadeId,long familiaId,string numero){ValidarTenantEntidade(tenantId,entidadeId); TenantId=tenantId; EntidadeId=entidadeId; FamiliaId=familiaId>0?familiaId:throw new ArgumentException("Cadastro social exige família.",nameof(familiaId)); NumeroCadastro=Required(numero,nameof(numero));} public long TenantId{get;} public long EntidadeId{get;} public long FamiliaId{get;} public string NumeroCadastro{get;} }
public sealed class SocialVulnerabilidade : SocialEntity { public SocialVulnerabilidade(long tenantId,long entidadeId,long? familiaId,long? socialPessoaId,SocialVulnerabilidadeTipo tipo){ValidarTenantEntidade(tenantId,entidadeId); if((familiaId??0)<=0 && (socialPessoaId??0)<=0) throw new ArgumentException("Vulnerabilidade exige família ou pessoa social.",nameof(familiaId)); TenantId=tenantId; EntidadeId=entidadeId; FamiliaId=familiaId; SocialPessoaId=socialPessoaId; Tipo=tipo;} public long TenantId{get;} public long EntidadeId{get;} public long? FamiliaId{get;} public long? SocialPessoaId{get;} public SocialVulnerabilidadeTipo Tipo{get;} }
public sealed class SocialPrograma : SocialEntity { public SocialPrograma(long tenantId,long entidadeId,string codigo,string nome,SocialProgramaTipo tipo){ValidarTenantEntidade(tenantId,entidadeId); Codigo=Required(codigo,nameof(codigo)); Nome=Required(nome,nameof(nome)); Tipo=tipo;} public string Codigo{get;} public string Nome{get;} public SocialProgramaTipo Tipo{get;} }
public sealed class SocialBeneficio : SocialEntity { public SocialBeneficio(long tenantId,long entidadeId,string nome,SocialBeneficioTipo tipo){ValidarTenantEntidade(tenantId,entidadeId); Nome=Required(nome,nameof(nome)); Tipo=tipo;} public string Nome{get;} public SocialBeneficioTipo Tipo{get;} }
public sealed class SocialBeneficioConcessao : SocialEntity { public SocialBeneficioConcessao(long tenantId,long entidadeId,long beneficioId,long? familiaId,long? socialPessoaId,SocialBeneficioStatus status,long? autorizadoBy=null){ValidarTenantEntidade(tenantId,entidadeId); BeneficioId=beneficioId>0?beneficioId:throw new ArgumentException("Benefício obrigatório.",nameof(beneficioId)); if((familiaId??0)<=0 && (socialPessoaId??0)<=0) throw new ArgumentException("Concessão exige família ou pessoa social.",nameof(familiaId)); if(status==SocialBeneficioStatus.CONCEDIDO && (autorizadoBy??0)<=0) throw new ArgumentException("Concessão concedida exige autorização.",nameof(autorizadoBy)); FamiliaId=familiaId; SocialPessoaId=socialPessoaId; Status=status; AutorizadoBy=autorizadoBy;} public long BeneficioId{get;} public long? FamiliaId{get;} public long? SocialPessoaId{get;} public SocialBeneficioStatus Status{get;} public long? AutorizadoBy{get;} }
public sealed class SocialAtendimento : SocialEntity { public SocialAtendimento(long tenantId,long entidadeId,string numero,string demanda){ValidarTenantEntidade(tenantId,entidadeId); NumeroAtendimento=Required(numero,nameof(numero)); Demanda=Required(demanda,nameof(demanda));} public string NumeroAtendimento{get;} public string Demanda{get;} }
public sealed class SocialEncaminhamento : SocialEntity { public SocialEncaminhamento(long tenantId,long entidadeId,string destino,string descricao){ValidarTenantEntidade(tenantId,entidadeId); Destino=Required(destino,nameof(destino)); Descricao=Required(descricao,nameof(descricao));} public string Destino{get;} public string Descricao{get;} }
public sealed class SocialVisita : SocialEntity { public SocialVisita(long tenantId,long entidadeId,string relato,decimal? latitude=null,decimal? longitude=null){ValidarTenantEntidade(tenantId,entidadeId); ValidarCoordenadas(latitude,longitude); Relato=Required(relato,nameof(relato)); Latitude=latitude; Longitude=longitude;} public string Relato{get;} public decimal? Latitude{get;} public decimal? Longitude{get;} }
public sealed class SocialParecer : SocialEntity { public SocialParecer(long tenantId,long entidadeId,string titulo,string texto,bool sigiloso=true){ValidarTenantEntidade(tenantId,entidadeId); Titulo=Required(titulo,nameof(titulo)); Texto=Required(texto,nameof(texto)); Sigiloso=sigiloso;} public string Titulo{get;} public string Texto{get;} public bool Sigiloso{get;} public bool DadoSensivel => Sigiloso; }
public sealed class SocialAcompanhamentoFamiliar : SocialEntity { public SocialAcompanhamentoFamiliar(long tenantId,long entidadeId,long familiaId,string objetivo){ValidarTenantEntidade(tenantId,entidadeId); FamiliaId=familiaId>0?familiaId:throw new ArgumentException("Família obrigatória.",nameof(familiaId)); Objetivo=Required(objetivo,nameof(objetivo));} public long FamiliaId{get;} public string Objetivo{get;} }
public sealed class SocialVigilanciaIndicador : SocialEntity { public SocialVigilanciaIndicador(long tenantId,long entidadeId,string codigo,string nome,decimal valor,bool permiteNegativo=false){ValidarTenantEntidade(tenantId,entidadeId); if(valor<0 && !permiteNegativo) throw new ArgumentException("Indicador não pode ter valor negativo.",nameof(valor)); Codigo=Required(codigo,nameof(codigo)); Nome=Required(nome,nameof(nome)); Valor=valor;} public string Codigo{get;} public string Nome{get;} public decimal Valor{get;} }
public sealed class SocialVigilanciaOcorrencia : SocialEntity { public SocialVigilanciaOcorrencia(long tenantId,long entidadeId,string tipo,string descricao,decimal? latitude=null,decimal? longitude=null){ValidarTenantEntidade(tenantId,entidadeId); ValidarCoordenadas(latitude,longitude); Tipo=Required(tipo,nameof(tipo)); Descricao=Required(descricao,nameof(descricao));} public string Tipo{get;} public string Descricao{get;} }
public sealed class SocialEvento : SocialEntity { public SocialEvento(long tenantId,long entidadeId,string tipo,string payloadJson){ValidarTenantEntidade(tenantId,entidadeId); Tipo=Required(tipo,nameof(tipo)); PayloadJson=Required(payloadJson,nameof(payloadJson));} public string Tipo{get;} public string PayloadJson{get;} }
