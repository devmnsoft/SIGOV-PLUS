using Sigov.Application.Defesa;
namespace Sigov.Web.Models.Defesa;
public sealed record DefesaListaViewModel(string Titulo,string Recurso,DefesaPagina Pagina,DefesaFiltro Filtro);
public sealed record DefesaFormViewModel(string Titulo,string Recurso,DefesaRegistroRequest Registro,IReadOnlyDictionary<string,IReadOnlyList<DefesaOpcao>> Opcoes);
