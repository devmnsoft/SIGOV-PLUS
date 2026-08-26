using Sigov.Application.Convenios;
namespace Sigov.Web.Models.Convenios;
public sealed record ConvenioListaViewModel(string Titulo,string Recurso,ConvenioPagina Pagina,ConvenioFiltro Filtro);
public sealed record ConvenioFormViewModel(string Titulo,string Recurso,long? Id,ConvenioRegistroRequest Registro,IReadOnlyDictionary<string,IReadOnlyList<ConvenioOpcao>> Opcoes);
public sealed record ConvenioDetalhesViewModel(string Titulo,string Recurso,long Id,ConvenioRegistroRequest Registro);
