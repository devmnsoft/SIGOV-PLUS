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
 [ValidateNever] public IReadOnlyList<DivergenciaResumo> Divergencias { get; set; }=[];
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
