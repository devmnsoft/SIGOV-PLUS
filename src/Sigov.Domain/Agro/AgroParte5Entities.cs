using Sigov.Domain.Agro.Enums;
using Sigov.Domain.Common;

namespace Sigov.Domain.Agro;

public sealed class EstradaVicinal : AggregateRoot
{
    public EstradaVicinal(long tenantId, long entidadeId, string codigo, string nome, AgroEstradaSituacao situacao, decimal? extensaoKm = null, decimal? latitudeInicio = null, decimal? longitudeInicio = null, decimal? latitudeFim = null, decimal? longitudeFim = null, string? geoJson = null)
    {
        AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (extensaoKm < 0) throw new ArgumentException("Extensão em km não pode ser negativa.", nameof(extensaoKm)); AgroParte5Guard.ValidarCoordenada(latitudeInicio, longitudeInicio); AgroParte5Guard.ValidarCoordenada(latitudeFim, longitudeFim); AgroParte5Guard.ValidarGeoJson(geoJson);
        TenantId = tenantId; EntidadeId = entidadeId; Codigo = AgroParte5Guard.Required(codigo, "Estrada exige código."); Nome = AgroParte5Guard.Required(nome, "Estrada exige nome."); Situacao = situacao; ExtensaoKm = extensaoKm;
    }
    public long TenantId { get; } public long EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public AgroEstradaSituacao Situacao { get; } public decimal? ExtensaoKm { get; } public bool ExigeAlertaDashboard => Situacao == AgroEstradaSituacao.INTERDITADA;
}

public sealed class PontoCriticoRural : AggregateRoot
{
    public PontoCriticoRural(long tenantId, long entidadeId, AgroPontoCriticoTipo tipoPonto, string descricao, string severidade, AgroPontoCriticoStatus status, decimal? latitude = null, decimal? longitude = null)
    { AgroParte5Guard.RequireTenant(tenantId, entidadeId); AgroParte5Guard.ValidarCoordenada(latitude, longitude); TenantId = tenantId; EntidadeId = entidadeId; TipoPonto = tipoPonto; Descricao = AgroParte5Guard.Required(descricao, "Ponto crítico exige descrição."); Severidade = AgroParte5Guard.Required(severidade, "Ponto crítico exige severidade."); Status = status; }
    public long TenantId { get; } public long EntidadeId { get; } public AgroPontoCriticoTipo TipoPonto { get; } public string Descricao { get; } public string Severidade { get; } public AgroPontoCriticoStatus Status { get; }
    public void ValidarEdicao(bool administrativo) { if (Status == AgroPontoCriticoStatus.RESOLVIDO && !administrativo) throw new InvalidOperationException("Ponto resolvido não pode ser editado sem permissão administrativa."); }
}

public sealed class OcorrenciaRural : AggregateRoot
{
    public OcorrenciaRural(long tenantId, long entidadeId, long? exercicioId, string numero, AgroOcorrenciaTipo tipo, string descricao, string severidade, AgroOcorrenciaStatus status)
    { AgroParte5Guard.RequireTenant(tenantId, entidadeId); TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; Numero = AgroParte5Guard.Required(numero, "Ocorrência exige número."); Tipo = tipo; Descricao = AgroParte5Guard.Required(descricao, "Ocorrência exige descrição."); Severidade = AgroParte5Guard.Required(severidade, "Ocorrência exige severidade."); Status = status; }
    public long TenantId { get; } public long EntidadeId { get; } public long? ExercicioId { get; } public string Numero { get; } public AgroOcorrenciaTipo Tipo { get; } public string Descricao { get; } public string Severidade { get; } public AgroOcorrenciaStatus Status { get; }
    public void ValidarEdicao(bool administrativo) { if (Status == AgroOcorrenciaStatus.RESOLVIDA && !administrativo) throw new InvalidOperationException("Ocorrência resolvida não pode ser alterada sem permissão administrativa."); }
}

public sealed class ManutencaoRural : AggregateRoot
{
    public ManutencaoRural(long tenantId, long entidadeId, long? exercicioId, string numero, string tipoManutencao, string descricao, AgroManutencaoRuralStatus status, DateOnly? dataExecucao = null, decimal? custoEstimado = null, decimal? custoRealizado = null)
    { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (custoEstimado < 0) throw new ArgumentException("Custo estimado não pode ser negativo.", nameof(custoEstimado)); if (custoRealizado < 0) throw new ArgumentException("Custo realizado não pode ser negativo.", nameof(custoRealizado)); if (status == AgroManutencaoRuralStatus.EXECUTADA && dataExecucao is null) throw new ArgumentException("Manutenção executada exige data de execução.", nameof(dataExecucao)); TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; Numero = AgroParte5Guard.Required(numero, "Manutenção exige número."); TipoManutencao = AgroParte5Guard.Required(tipoManutencao, "Manutenção exige tipo."); Descricao = AgroParte5Guard.Required(descricao, "Manutenção exige descrição."); Status = status; }
    public long TenantId { get; } public long EntidadeId { get; } public long? ExercicioId { get; } public string Numero { get; } public string TipoManutencao { get; } public string Descricao { get; } public AgroManutencaoRuralStatus Status { get; }
    public void Executar() { if (Status == AgroManutencaoRuralStatus.CANCELADA) throw new InvalidOperationException("Manutenção cancelada não pode ser executada."); }
}

public sealed class FeiraRural : AggregateRoot
{
    public FeiraRural(long tenantId, long entidadeId, string codigo, string nome, string local, AgroFeiraSituacao situacao) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); TenantId = tenantId; EntidadeId = entidadeId; Codigo = AgroParte5Guard.Required(codigo, "Feira exige código."); Nome = AgroParte5Guard.Required(nome, "Feira exige nome."); Local = AgroParte5Guard.Required(local, "Feira exige local."); Situacao = situacao; }
    public long TenantId { get; } public long EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public string Local { get; } public AgroFeiraSituacao Situacao { get; }
    public void ValidarNovoFeirante() { if (Situacao != AgroFeiraSituacao.ATIVA) throw new InvalidOperationException("Feira inativa não recebe novo feirante."); }
}

public sealed class Feirante : AggregateRoot
{
    public Feirante(long tenantId, long entidadeId, long feiraId, long? produtorId, long? pessoaId, string numeroCadastro, bool autorizado, DateOnly? validadeAutorizacao, AgroFeiranteSituacao situacao) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (feiraId <= 0) throw new ArgumentException("Feira é obrigatória.", nameof(feiraId)); if (!produtorId.HasValue && !pessoaId.HasValue) throw new ArgumentException("Feirante exige produtor ou pessoa."); TenantId = tenantId; EntidadeId = entidadeId; FeiraId = feiraId; ProdutorId = produtorId; PessoaId = pessoaId; NumeroCadastro = AgroParte5Guard.Required(numeroCadastro, "Número de cadastro é obrigatório."); Autorizado = autorizado; ValidadeAutorizacao = validadeAutorizacao; Situacao = situacao; }
    public long TenantId { get; } public long EntidadeId { get; } public long FeiraId { get; } public long? ProdutorId { get; } public long? PessoaId { get; } public string NumeroCadastro { get; } public bool Autorizado { get; } public DateOnly? ValidadeAutorizacao { get; } public AgroFeiranteSituacao Situacao { get; } public bool AutorizacaoVencida(DateOnly hoje) => ValidadeAutorizacao.HasValue && ValidadeAutorizacao.Value < hoje;
    public void ValidarBancaAtiva() { if (!Autorizado) throw new InvalidOperationException("Feirante não autorizado não pode receber banca ativa."); }
}

public sealed class BancaFeira : AggregateRoot { public BancaFeira(long tenantId, long entidadeId, long feiraId, long? feiranteId, string numeroBanca, string status) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (feiraId <= 0) throw new ArgumentException("Feira é obrigatória.", nameof(feiraId)); TenantId = tenantId; EntidadeId = entidadeId; FeiraId = feiraId; FeiranteId = feiranteId; NumeroBanca = AgroParte5Guard.Required(numeroBanca, "Número da banca é obrigatório."); Status = AgroParte5Guard.Required(status, "Status da banca é obrigatório."); } public long TenantId { get; } public long EntidadeId { get; } public long FeiraId { get; } public long? FeiranteId { get; } public string NumeroBanca { get; } public string Status { get; } }
public sealed class AutorizacaoFeira : AggregateRoot { public AutorizacaoFeira(long tenantId, long entidadeId, long feiraId, long feiranteId, string numero, AgroAutorizacaoFeiraStatus status) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (feiraId <= 0 || feiranteId <= 0) throw new ArgumentException("Feira e feirante são obrigatórios."); TenantId = tenantId; EntidadeId = entidadeId; FeiraId = feiraId; FeiranteId = feiranteId; Numero = AgroParte5Guard.Required(numero, "Autorização exige número."); Status = status; } public long TenantId { get; } public long EntidadeId { get; } public long FeiraId { get; } public long FeiranteId { get; } public string Numero { get; } public AgroAutorizacaoFeiraStatus Status { get; } }

public sealed class Agroindustria : AggregateRoot { public Agroindustria(long tenantId, long entidadeId, string codigo, string nome, string tipoAtividade, AgroAgroindustriaSituacao situacao) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); TenantId = tenantId; EntidadeId = entidadeId; Codigo = AgroParte5Guard.Required(codigo, "Agroindústria exige código."); Nome = AgroParte5Guard.Required(nome, "Agroindústria exige nome."); TipoAtividade = AgroParte5Guard.Required(tipoAtividade, "Agroindústria exige atividade."); Situacao = situacao; } public long TenantId { get; } public long EntidadeId { get; } public string Codigo { get; } public string Nome { get; } public string TipoAtividade { get; } public AgroAgroindustriaSituacao Situacao { get; } }
public sealed class InspecaoMunicipal : AggregateRoot { public InspecaoMunicipal(long tenantId, long entidadeId, long agroindustriaId, string numero, DateOnly dataInspecao, AgroInspecaoResultado resultado, bool possuiExigencias, DateOnly? prazoAdequacao) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (agroindustriaId <= 0) throw new ArgumentException("Inspeção exige agroindústria.", nameof(agroindustriaId)); if (dataInspecao == default) throw new ArgumentException("Inspeção exige data.", nameof(dataInspecao)); if (possuiExigencias && prazoAdequacao is null) throw new ArgumentException("Inspeção com exigências deve ter prazo de adequação.", nameof(prazoAdequacao)); TenantId = tenantId; EntidadeId = entidadeId; AgroindustriaId = agroindustriaId; Numero = AgroParte5Guard.Required(numero, "Inspeção exige número."); DataInspecao = dataInspecao; Resultado = resultado; } public long TenantId { get; } public long EntidadeId { get; } public long AgroindustriaId { get; } public string Numero { get; } public DateOnly DataInspecao { get; } public AgroInspecaoResultado Resultado { get; } }

public sealed class CompraAgriculturaFamiliar : AggregateRoot
{
    public CompraAgriculturaFamiliar(long tenantId, long entidadeId, long? exercicioId, long produtorId, string produto, decimal quantidade, string unidadeMedida, decimal valorUnitario, AgroCompraAgriculturaFamiliarStatus status) { AgroParte5Guard.RequireTenant(tenantId, entidadeId); if (produtorId <= 0) throw new ArgumentException("Compra exige produtor.", nameof(produtorId)); if (quantidade <= 0) throw new ArgumentException("Quantidade deve ser maior que zero.", nameof(quantidade)); if (valorUnitario < 0) throw new ArgumentException("Valor unitário não pode ser negativo.", nameof(valorUnitario)); TenantId = tenantId; EntidadeId = entidadeId; ExercicioId = exercicioId; ProdutorId = produtorId; Produto = AgroParte5Guard.Required(produto, "Compra exige produto."); Quantidade = quantidade; UnidadeMedida = AgroParte5Guard.Required(unidadeMedida, "Unidade de medida é obrigatória."); ValorUnitario = valorUnitario; ValorTotal = quantidade * valorUnitario; Status = status; }
    public long TenantId { get; } public long EntidadeId { get; } public long? ExercicioId { get; } public long ProdutorId { get; } public string Produto { get; } public decimal Quantidade { get; } public string UnidadeMedida { get; } public decimal ValorUnitario { get; } public decimal ValorTotal { get; } public AgroCompraAgriculturaFamiliarStatus Status { get; }
    public void ValidarIntegracao() { if (Status == AgroCompraAgriculturaFamiliarStatus.CANCELADA) throw new InvalidOperationException("Compra cancelada não pode integrar com compras/financeiro."); }
}

internal static class AgroParte5Guard
{
    public static void RequireTenant(long tenantId, long entidadeId) { if (tenantId <= 0) throw new ArgumentException("Tenant é obrigatório.", nameof(tenantId)); if (entidadeId <= 0) throw new ArgumentException("Entidade é obrigatória.", nameof(entidadeId)); }
    public static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException(message) : value.Trim();
    public static void ValidarCoordenada(decimal? latitude, decimal? longitude) { if (latitude is < -90m or > 90m) throw new ArgumentException("Latitude deve estar entre -90 e 90.", nameof(latitude)); if (longitude is < -180m or > 180m) throw new ArgumentException("Longitude deve estar entre -180 e 180.", nameof(longitude)); if (latitude.HasValue != longitude.HasValue) throw new ArgumentException("Latitude e longitude devem ser informadas em conjunto."); }
    public static void ValidarGeoJson(string? geoJson) { if (!string.IsNullOrWhiteSpace(geoJson) && !geoJson.TrimStart().StartsWith("{", StringComparison.Ordinal)) throw new ArgumentException("GeoJSON inválido.", nameof(geoJson)); }

}
