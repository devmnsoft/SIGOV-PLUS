using Sigov.Application.Transito;
namespace Sigov.Web.Models.Transito;
public sealed record TransitoListaViewModel(string Titulo,string Recurso,TransitoPagina Pagina,TransitoFiltro Filtro);
public sealed record TransitoFormViewModel(string Titulo,string Recurso,TransitoRegistroRequest Registro,IReadOnlyDictionary<string,IReadOnlyList<TransitoOpcao>> Opcoes);
