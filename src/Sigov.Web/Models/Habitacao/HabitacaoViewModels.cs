using Sigov.Application.Habitacao;
namespace Sigov.Web.Models.Habitacao;
public sealed record HabitacaoListaViewModel(string Titulo,string Recurso,HabitacaoPagina Resultado,HabitacaoFiltro Filtro);
