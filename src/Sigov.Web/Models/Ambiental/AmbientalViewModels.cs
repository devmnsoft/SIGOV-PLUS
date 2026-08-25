using Sigov.Application.Ambiental;
namespace Sigov.Web.Models.Ambiental;
public sealed record AmbientalListaViewModel(string Titulo,string Recurso,AmbientalPagina Pagina,AmbientalFiltro Filtro);
