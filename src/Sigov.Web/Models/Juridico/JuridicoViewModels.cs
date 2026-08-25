using Sigov.Application.Juridico;
namespace Sigov.Web.Models.Juridico;
public sealed record JuridicoListaViewModel(string Titulo,string Recurso,JuridicoPagina Resultado,JuridicoFiltro Filtro,bool PermiteAlteracao);
