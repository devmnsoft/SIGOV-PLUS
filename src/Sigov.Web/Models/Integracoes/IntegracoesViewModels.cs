using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Integracoes;

public sealed class IntegracaoSistemaFormViewModel { [Required] public string Codigo { get; set; } = string.Empty; [Required] public string Nome { get; set; } = string.Empty; public string TipoIntegracao { get; set; } = "API_EXTERNA"; public string Ambiente { get; set; } = "DEVELOPMENT"; public string? BaseUrl { get; set; } public bool Ativo { get; set; } = true; }
public sealed class ApiCredentialFormViewModel { [Required] public string Nome { get; set; } = string.Empty; [Required] public string ClientId { get; set; } = string.Empty; public string? Descricao { get; set; } public string Scopes { get; set; } = "integracao.webhook.receber"; }
public sealed class WebhookRecebidoViewModel { public long Id { get; set; } public string Origem { get; set; } = string.Empty; public string Evento { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; }
public sealed class WebhookEnviadoViewModel { public long Id { get; set; } public string Destino { get; set; } = string.Empty; public string Url { get; set; } = string.Empty; public string Evento { get; set; } = string.Empty; public string Status { get; set; } = string.Empty; }
public sealed class OutboxEventoViewModel { public long Id { get; set; } public string TipoEvento { get; set; } = string.Empty; public string Status { get; set; } = "PENDENTE"; public int Tentativas { get; set; } public bool DeadLetter { get; set; } }
public sealed class RemessaOficialFormViewModel { [Required] public string TipoRemessa { get; set; } = "TCE"; public string? Competencia { get; set; } [Required] public string Numero { get; set; } = string.Empty; }
public sealed class CertificadoDigitalFormViewModel { [Required] public string Nome { get; set; } = string.Empty; public string TipoCertificado { get; set; } = "ESTRUTURAL"; public DateOnly? ValidadeFim { get; set; } public string? StorageKey { get; set; } public string? Thumbprint { get; set; } }
public sealed class GovBrConfiguracaoViewModel { public string Ambiente { get; set; } = "DEVELOPMENT"; public string? ClientId { get; set; } public string? RedirectUri { get; set; } }
public sealed class IntegracaoDashboardViewModel { public long TotalSistemas { get; set; } public long OutboxPendentes { get; set; } public long WebhooksRecebidosHoje { get; set; } public long RemessasPendentes { get; set; } }
