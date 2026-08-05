namespace Sigov.Web.Models.Busca;

public sealed record BuscaSugestaoViewModel(string Area, string Titulo, string Descricao, string Url, string Icon, string Badge, string Atalho = "");
public sealed record BuscaSugestoesResponse(IReadOnlyCollection<BuscaSugestaoViewModel> Resultados, string Fonte, string CorrelationId);
