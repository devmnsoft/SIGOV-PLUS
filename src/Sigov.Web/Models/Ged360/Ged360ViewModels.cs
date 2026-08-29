namespace Sigov.Web.Models.Ged360;

public sealed record GedDashboardViewModel(long Documentos, long PendentesClassificacao, long OcrPendente, long OcrBaixaConfianca, long ProtocolosTramitacao, long FluxosAtrasados, long AssinaturasPendentes, long EmprestimosVencidos, long Caixas, long TemporalidadeProxima, long EliminacoesPendentes, long AcessosSensiveis);
public sealed record GedDocumentoListItem(long Id, string Titulo, string Status, string Confidencialidade, DateTimeOffset CriadoEm, string Tipo, string Classificacao);
