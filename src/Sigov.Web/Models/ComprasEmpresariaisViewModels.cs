using System.ComponentModel.DataAnnotations;
using Sigov.Application.Common;
using Sigov.Application.ComprasEmpresariais;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Sigov.Web.Models;

public sealed class InspecaoRecebimentoViewModel
{
 [ValidateNever] public required RecebimentoDetalhe Recebimento { get; init; }
 public long Version { get; set; }
 [StringLength(1000)] public string? Justificativa { get; set; }
 public List<InspecaoDecisaoViewModel> Itens { get; set; }=[];
 public bool Conflito { get; set; }
 public string? MensagemConflito { get; set; }
 public long? VersaoAtual { get; set; }
 public string? ReturnUrl { get; set; }
 [ValidateNever] public IReadOnlyCollection<DivergenciaResumo> Divergencias { get; set; }=[];
 [ValidateNever] public IReadOnlyList<DestinacaoRejeitadoItem> Destinacoes { get; set; }=[];
 [ValidateNever] public IReadOnlyList<DevolucaoResumo> DevolucoesVinculadas { get; set; }=[];
}
public sealed class InspecaoDecisaoViewModel
{
 public long RecebimentoItemId { get; set; }
 [Required(ErrorMessage="Informe a quantidade aceita.")] public string QuantidadeAceita { get; set; }=string.Empty;
 [Required(ErrorMessage="Informe a quantidade rejeitada.")] public string QuantidadeRejeitada { get; set; }=string.Empty;
}

public sealed class DivergenciaDetalheViewModel
{
 public required DivergenciaDetalhe Divergencia { get; init; }
 public required IReadOnlyList<ResponsavelDivergencia> Responsaveis { get; init; }
 public string? ReturnUrl { get; init; }
 public string IdempotencyKey { get; init; }=Guid.NewGuid().ToString("N");
}

public sealed class DevolucaoItemLinhaViewModel
{
 public long RecebimentoItemId { get; set; }
 public string Produto { get; set; } = string.Empty;
 public string Unidade { get; set; } = string.Empty;
 public decimal QuantidadeRejeitada { get; set; }
 public decimal QuantidadeReservada { get; set; }
 public decimal QuantidadeExpedida { get; set; }
 public decimal QuantidadeEntregue { get; set; }
 public decimal SaldoElegivel { get; set; }
 public bool Selecionado { get; set; }
 public string QuantidadeDevolver { get; set; } = string.Empty;
}

public sealed class DevolucaoFormViewModel
{
 public Guid RecebimentoId { get; set; }
 public string DocumentoRecebimento { get; set; } = string.Empty;
 public string Fornecedor { get; set; } = string.Empty;
 [Required(ErrorMessage="Informe o motivo da devolução."), StringLength(2000, ErrorMessage="O motivo deve ter no máximo 2000 caracteres.")]
 public string Motivo { get; set; } = string.Empty;
 [Required(ErrorMessage="Selecione um responsável elegível.")]
 public Guid ResponsavelId { get; set; }
 [Required(ErrorMessage="Informe o local físico de origem."), StringLength(200, ErrorMessage="A origem física deve ter no máximo 200 caracteres.")]
 public string OrigemFisica { get; set; } = string.Empty;
 [Required(ErrorMessage="Informe o destino no fornecedor."), StringLength(200, ErrorMessage="O destino deve ter no máximo 200 caracteres.")]
 public string Destino { get; set; } = string.Empty;
 public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
 public long? DevolucaoId { get; set; }
 public long? Version { get; set; }
 public bool IsEdicao => DevolucaoId.HasValue;
 public string? ReturnUrl { get; set; }

 [ValidateNever] public ContextoInstitucionalSnapshot? ContextoInstitucional { get; set; }
 [ValidateNever] public IReadOnlyList<ResponsavelDivergencia> Responsaveis { get; set; } = [];
 public List<DevolucaoItemLinhaViewModel> Itens { get; set; } = [];

 public bool Conflito { get; set; }
 public string? MensagemConflito { get; set; }
 public long? VersaoAtual { get; set; }
}

public sealed class NovaFaturaItemFormModel
{
    public long PedidoItemId { get; set; }
    public bool Selecionado { get; set; }
    public string ProdutoNome { get; set; } = string.Empty;
    public string Unidade { get; set; } = string.Empty;
    public decimal QuantidadePedida { get; set; }
    public decimal QuantidadeCancelada { get; set; }
    public decimal QuantidadeVigente { get; set; }
    public decimal QuantidadeAceitaTotal { get; set; }
    public decimal QuantidadeAprovadaOutras { get; set; }
    public decimal QuantidadeReservadaOutras { get; set; }
    public decimal SaldoDisponivel { get; set; }
    public decimal ValorUnitarioPedido { get; set; }
    public string Quantidade { get; set; } = string.Empty;
    public string ValorUnitario { get; set; } = string.Empty;
    public string? MotivoSaldoZero { get; set; }
}

public sealed class NovaFaturaFormViewModel
{
    [Required(ErrorMessage = "O pedido é obrigatório.")]
    public Guid PedidoId { get; set; }

    [Required(ErrorMessage = "O fornecedor é obrigatório.")]
    public Guid FornecedorId { get; set; }

    public string PedidoNumero { get; set; } = string.Empty;
    public string FornecedorNome { get; set; } = string.Empty;
    public string FornecedorCnpj { get; set; } = string.Empty;
    public decimal TotalPedido { get; set; }

    [Required(ErrorMessage = "O número do documento é obrigatório."), StringLength(50, ErrorMessage = "O número deve ter no máximo 50 caracteres.")]
    public string Numero { get; set; } = string.Empty;

    [Required(ErrorMessage = "A série é obrigatória."), StringLength(20, ErrorMessage = "A série deve ter no máximo 20 caracteres.")]
    public string Serie { get; set; } = "1";

    [Required(ErrorMessage = "O tipo de documento é obrigatório."), StringLength(30)]
    public string TipoDocumento { get; set; } = "NOTA_FISCAL";

    [StringLength(60, ErrorMessage = "A chave de acesso deve ter no máximo 60 caracteres.")]
    public string? ChaveAcesso { get; set; }

    public string? DataEmissao { get; set; }
    public string? DataVencimento { get; set; }

    public string ValorDesconto { get; set; } = "0,00";
    public string ValorFrete { get; set; } = "0,00";
    public string ValorSeguro { get; set; } = "0,00";
    public string ValorOutrasDespesas { get; set; } = "0,00";

    [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
    public string? Observacoes { get; set; }

    [Required]
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");

    public bool RegistrarComDivergencia { get; set; }

    public List<NovaFaturaItemFormModel> Itens { get; set; } = [];

    public string? BuscaPedido { get; set; }
    public int PaginaPedidos { get; set; } = 1;
    [ValidateNever] public PagedResult<PedidoResumo>? PedidosElegiveis { get; set; }
}

