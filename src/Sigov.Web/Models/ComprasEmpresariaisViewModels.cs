using System.ComponentModel.DataAnnotations;
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
