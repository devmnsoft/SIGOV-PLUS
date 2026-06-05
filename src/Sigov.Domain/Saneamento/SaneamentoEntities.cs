using Sigov.Domain.Common;

namespace Sigov.Domain.Saneamento;

public enum TipoConsumidorSaneamento { PessoaFisica, PessoaJuridica, Publico }
public enum SituacaoConsumidorSaneamento { Ativo, Inativo, Suspenso }
public enum TipoLigacaoSaneamento { Agua, Esgoto, AguaEsgoto }
public enum SituacaoLigacaoSaneamento { Ativa, Cortada, Suspensa, Inativa }
public enum CategoriaLigacaoSaneamento { Residencial, Comercial, Industrial, Publica, Social }
public enum SituacaoUnidadeConsumidora { Ativa, Inativa, Suspensa }
public enum SituacaoHidrometro { Instalado, Retirado, Manutencao, Inativo }
public enum TipoLeitura { Normal, Ajuste, Estimada, Revisao }
public enum FaturaSaneamentoStatus { Aberta, Paga, Vencida, Cancelada, Parcelada }
public enum FormaPagamentoSaneamento { Dinheiro, Cartao, Boleto, Pix, ManualDev }
public enum ParcelamentoSaneamentoStatus { Ativo, Quitado, Cancelado, Atrasado }
public enum OrdemServicoSaneamentoStatus { Aberta, Agendada, EmCampo, Executada, Cancelada, NaoExecutada }
public enum PrioridadeOrdemServico { Baixa, Media, Alta, Urgente }
public enum LaboratorioAmostraStatus { Coletada, EmAnalise, Concluida, Cancelada }
public enum TipoRedeSaneamento { Agua, Esgoto, Drenagem }
public enum SituacaoRedeSaneamento { Ativa, Inativa, Manutencao }
public enum OcorrenciaOperacionalStatus { Aberta, EmAtendimento, Resolvida, Cancelada }

public abstract class SaneamentoEntity : AggregateRoot
{
    protected static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
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

public sealed class SaneamentoConsumidor : SaneamentoEntity
{
    public SaneamentoConsumidor(long tenantId, long entidadeId, long pessoaId, string codigoConsumidor)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        TenantId = tenantId; EntidadeId = entidadeId;
        PessoaId = pessoaId > 0 ? pessoaId : throw new ArgumentException("Consumidor deve estar vinculado a uma pessoa.", nameof(pessoaId));
        CodigoConsumidor = Required(codigoConsumidor, nameof(codigoConsumidor));
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long PessoaId { get; }
    public string CodigoConsumidor { get; }
}

public sealed class SaneamentoLigacao : SaneamentoEntity
{
    public SaneamentoLigacao(long tenantId, long entidadeId, long consumidorId, string numeroLigacao)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        TenantId = tenantId; EntidadeId = entidadeId;
        ConsumidorId = consumidorId > 0 ? consumidorId : throw new ArgumentException("Ligação deve pertencer a um consumidor.", nameof(consumidorId));
        NumeroLigacao = Required(numeroLigacao, nameof(numeroLigacao));
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long ConsumidorId { get; }
    public string NumeroLigacao { get; }
}

public sealed partial class UnidadeConsumidora : SaneamentoEntity
{
    public UnidadeConsumidora(long tenantId, long entidadeId, long consumidorId, string codigoUnidade, decimal? latitude = null, decimal? longitude = null)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        ValidarCoordenadas(latitude, longitude);
        TenantId = tenantId; EntidadeId = entidadeId;
        ConsumidorId = consumidorId > 0 ? consumidorId : throw new ArgumentException("Unidade consumidora deve estar vinculada a consumidor.", nameof(consumidorId));
        CodigoUnidade = Required(codigoUnidade, nameof(codigoUnidade));
        Nome = CodigoUnidade;
        Latitude = latitude; Longitude = longitude;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long ConsumidorId { get; }
    public string CodigoUnidade { get; }
    public decimal? Latitude { get; }
    public decimal? Longitude { get; }
}

public sealed class Hidrometro : SaneamentoEntity
{
    public Hidrometro(long tenantId, long entidadeId, long unidadeConsumidoraId, string numeroSerie)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        TenantId = tenantId; EntidadeId = entidadeId;
        UnidadeConsumidoraId = unidadeConsumidoraId > 0 ? unidadeConsumidoraId : throw new ArgumentException("Hidrômetro deve pertencer a uma unidade consumidora.", nameof(unidadeConsumidoraId));
        NumeroSerie = Required(numeroSerie, nameof(numeroSerie));
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long UnidadeConsumidoraId { get; }
    public string NumeroSerie { get; }
}

public sealed class LeituraConsumo : SaneamentoEntity
{
    public LeituraConsumo(long tenantId, long entidadeId, long unidadeConsumidoraId, decimal leituraAnterior, decimal leituraAtual, decimal consumoFaturado, TipoLeitura tipoLeitura, string? justificativaAjuste = null, decimal? latitude = null, decimal? longitude = null)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        ValidarCoordenadas(latitude, longitude);
        if (unidadeConsumidoraId <= 0) throw new ArgumentException("Leitura deve pertencer a uma unidade consumidora.", nameof(unidadeConsumidoraId));
        if (leituraAtual < leituraAnterior && (tipoLeitura != TipoLeitura.Ajuste || string.IsNullOrWhiteSpace(justificativaAjuste))) throw new ArgumentException("Leitura atual não pode ser menor que a anterior sem ajuste justificado.", nameof(leituraAtual));
        if (consumoFaturado < 0) throw new ArgumentException("Consumo faturado não pode ser negativo.", nameof(consumoFaturado));
        TenantId = tenantId; EntidadeId = entidadeId; UnidadeConsumidoraId = unidadeConsumidoraId;
        LeituraAnterior = leituraAnterior; LeituraAtual = leituraAtual; ConsumoMedido = leituraAtual - leituraAnterior; ConsumoFaturado = consumoFaturado; TipoLeitura = tipoLeitura;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long UnidadeConsumidoraId { get; }
    public decimal LeituraAnterior { get; }
    public decimal LeituraAtual { get; }
    public decimal ConsumoMedido { get; }
    public decimal ConsumoFaturado { get; }
    public TipoLeitura TipoLeitura { get; }
}

public sealed class FaturaSaneamento : SaneamentoEntity
{
    public FaturaSaneamento(long tenantId, long entidadeId, long unidadeConsumidoraId, string numero, decimal valorTotal)
    {
        ValidarTenantEntidade(tenantId, entidadeId);
        if (unidadeConsumidoraId <= 0) throw new ArgumentException("Fatura deve pertencer a uma unidade consumidora.", nameof(unidadeConsumidoraId));
        if (valorTotal < 0) throw new ArgumentException("Valor total deve ser maior ou igual a zero.", nameof(valorTotal));
        TenantId = tenantId; EntidadeId = entidadeId; UnidadeConsumidoraId = unidadeConsumidoraId; Numero = Required(numero, nameof(numero)); ValorTotal = valorTotal;
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public long UnidadeConsumidoraId { get; }
    public string Numero { get; }
    public decimal ValorTotal { get; }
    public decimal ValorPago { get; private set; }
    public FaturaSaneamentoStatus Status { get; private set; } = FaturaSaneamentoStatus.Aberta;
    public decimal Saldo => ValorTotal - ValorPago;
    public void Cancelar() => Status = FaturaSaneamentoStatus.Cancelada;
    public void RegistrarPagamento(decimal valor)
    {
        if (Status == FaturaSaneamentoStatus.Paga) throw new InvalidOperationException("Fatura paga não pode receber novo pagamento.");
        if (Status == FaturaSaneamentoStatus.Cancelada) throw new InvalidOperationException("Fatura cancelada não pode ser paga.");
        if (valor <= 0) throw new ArgumentException("Valor do pagamento deve ser positivo.", nameof(valor));
        if (valor > Saldo) throw new InvalidOperationException("Pagamento não pode ultrapassar saldo da fatura.");
        ValorPago += valor;
        if (Saldo == 0) Status = FaturaSaneamentoStatus.Paga;
    }
}

public sealed class FaturaSaneamentoItem : SaneamentoEntity { public FaturaSaneamentoItem(long faturaId, string descricao, decimal valorTotal) { FaturaId = faturaId > 0 ? faturaId : throw new ArgumentException("Item deve pertencer a uma fatura.", nameof(faturaId)); Descricao = Required(descricao, nameof(descricao)); ValorTotal = valorTotal >= 0 ? valorTotal : throw new ArgumentException("Valor inválido.", nameof(valorTotal)); } public long FaturaId { get; } public string Descricao { get; } public decimal ValorTotal { get; } }
public sealed class ArrecadacaoSaneamento : SaneamentoEntity { public ArrecadacaoSaneamento(long faturaId, decimal valorPago) { FaturaId = faturaId > 0 ? faturaId : throw new ArgumentException("Arrecadação deve pertencer a uma fatura.", nameof(faturaId)); ValorPago = valorPago > 0 ? valorPago : throw new ArgumentException("Pagamento deve ser positivo.", nameof(valorPago)); } public long FaturaId { get; } public decimal ValorPago { get; } }
public sealed class ParcelamentoSaneamento : SaneamentoEntity { public ParcelamentoSaneamento(long consumidorId, int quantidadeParcelas, decimal valorTotal) { ConsumidorId = consumidorId > 0 ? consumidorId : throw new ArgumentException("Parcelamento deve pertencer a um consumidor.", nameof(consumidorId)); QuantidadeParcelas = quantidadeParcelas > 0 ? quantidadeParcelas : throw new ArgumentException("Quantidade de parcelas deve ser maior que zero.", nameof(quantidadeParcelas)); ValorTotal = valorTotal >= 0 ? valorTotal : throw new ArgumentException("Valor inválido.", nameof(valorTotal)); } public long ConsumidorId { get; } public int QuantidadeParcelas { get; } public decimal ValorTotal { get; } }
public sealed class ParcelamentoSaneamentoItem : SaneamentoEntity { public ParcelamentoSaneamentoItem(long parcelamentoId, int parcela, decimal valor) { ParcelamentoId = parcelamentoId > 0 ? parcelamentoId : throw new ArgumentException("Parcela deve pertencer a um parcelamento.", nameof(parcelamentoId)); Parcela = parcela > 0 ? parcela : throw new ArgumentException("Número da parcela inválido.", nameof(parcela)); Valor = valor >= 0 ? valor : throw new ArgumentException("Valor inválido.", nameof(valor)); } public long ParcelamentoId { get; } public int Parcela { get; } public decimal Valor { get; } }

public sealed class OrdemServicoSaneamento : SaneamentoEntity
{
    public OrdemServicoSaneamento(long tenantId, long entidadeId, string numero, string tipoServico, string descricao, decimal? latitude = null, decimal? longitude = null)
    {
        ValidarTenantEntidade(tenantId, entidadeId); ValidarCoordenadas(latitude, longitude);
        TenantId = tenantId; EntidadeId = entidadeId; Numero = Required(numero, nameof(numero)); TipoServico = Required(tipoServico, nameof(tipoServico)); Descricao = Required(descricao, nameof(descricao));
    }
    public long TenantId { get; }
    public long EntidadeId { get; }
    public string Numero { get; }
    public string TipoServico { get; }
    public string Descricao { get; }
    public OrdemServicoSaneamentoStatus Status { get; private set; } = OrdemServicoSaneamentoStatus.Aberta;
    public void Cancelar() => Status = OrdemServicoSaneamentoStatus.Cancelada;
    public void Executar(string solucao)
    {
        if (Status == OrdemServicoSaneamentoStatus.Cancelada) throw new InvalidOperationException("Ordem cancelada não pode ser executada.");
        Status = OrdemServicoSaneamentoStatus.Executada; Solucao = Required(solucao, nameof(solucao));
    }
    public void Editar(bool permissaoAdministrativa)
    {
        if (Status == OrdemServicoSaneamentoStatus.Executada && !permissaoAdministrativa) throw new InvalidOperationException("Ordem executada não pode ser editada sem permissão administrativa.");
    }
    public string? Solucao { get; private set; }
}

public sealed class EquipeCampoSaneamento : SaneamentoEntity { public EquipeCampoSaneamento(string codigo, string nome) { Codigo = Required(codigo, nameof(codigo)); Nome = Required(nome, nameof(nome)); } public string Codigo { get; } public string Nome { get; } }
public sealed class ServicoExecutadoSaneamento : SaneamentoEntity { public ServicoExecutadoSaneamento(long ordemServicoId, string descricao) { OrdemServicoId = ordemServicoId > 0 ? ordemServicoId : throw new ArgumentException("Serviço executado deve pertencer a uma ordem de serviço.", nameof(ordemServicoId)); Descricao = Required(descricao, nameof(descricao)); } public long OrdemServicoId { get; } public string Descricao { get; } }
public sealed class LaboratorioAmostra : SaneamentoEntity { public LaboratorioAmostra(string numero, string pontoColeta, DateTimeOffset dataColeta, decimal? latitude = null, decimal? longitude = null) { ValidarCoordenadas(latitude, longitude); Numero = Required(numero, nameof(numero)); PontoColeta = Required(pontoColeta, nameof(pontoColeta)); DataColeta = dataColeta == default ? throw new ArgumentException("Data da coleta é obrigatória.", nameof(dataColeta)) : dataColeta; } public string Numero { get; } public string PontoColeta { get; } public DateTimeOffset DataColeta { get; } }
public sealed class LaboratorioResultado : SaneamentoEntity { public LaboratorioResultado(long amostraId, string parametro, string valor) { AmostraId = amostraId > 0 ? amostraId : throw new ArgumentException("Resultado deve pertencer a uma amostra.", nameof(amostraId)); Parametro = Required(parametro, nameof(parametro)); Valor = Required(valor, nameof(valor)); } public long AmostraId { get; } public string Parametro { get; } public string Valor { get; } }
public sealed class RedeSaneamentoTrecho : SaneamentoEntity { public RedeSaneamentoTrecho(string codigo, TipoRedeSaneamento tipoRede, string? geometriaGeoJson = null) { Codigo = Required(codigo, nameof(codigo)); TipoRede = tipoRede; GeometriaGeoJson = geometriaGeoJson; } public string Codigo { get; } public TipoRedeSaneamento TipoRede { get; } public string? GeometriaGeoJson { get; } }
public sealed class OcorrenciaOperacionalSaneamento : SaneamentoEntity { public OcorrenciaOperacionalSaneamento(string tipoOcorrencia, string descricao, decimal? latitude = null, decimal? longitude = null) { ValidarCoordenadas(latitude, longitude); TipoOcorrencia = Required(tipoOcorrencia, nameof(tipoOcorrencia)); Descricao = Required(descricao, nameof(descricao)); } public string TipoOcorrencia { get; } public string Descricao { get; } }
public sealed class SaneamentoEvento : SaneamentoEntity { public SaneamentoEvento(string tipoEvento, string payloadJson) { TipoEvento = Required(tipoEvento, nameof(tipoEvento)); PayloadJson = Required(payloadJson, nameof(payloadJson)); } public string TipoEvento { get; } public string PayloadJson { get; } }
