using Sigov.Application.Atendimento;
namespace Sigov.Web.Models.Atendimento;
public sealed record AtendimentoListaViewModel(string Titulo, string Recurso, AtendimentoPagina Pagina, AtendimentoFiltro Filtro);
