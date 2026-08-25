using Sigov.Application.Obras.Engenharia;
namespace Sigov.Web.Models.Obras;
public sealed record ObrasListaViewModel(string Titulo,string Recurso,IReadOnlyList<ObrasRegistro> Items,ObrasFiltro Filtro);
