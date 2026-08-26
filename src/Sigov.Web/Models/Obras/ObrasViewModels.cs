using System.ComponentModel.DataAnnotations;
using Sigov.Application.Obras.Engenharia;
namespace Sigov.Web.Models.Obras;
public sealed record ObrasListaViewModel(string Titulo,string Recurso,IReadOnlyList<ObrasRegistro> Items,ObrasFiltro Filtro);
public sealed class ObrasRegistroFormViewModel
{
 [Range(1,long.MaxValue,ErrorMessage="Selecione uma obra.")] public long ObraId { get; set; }
 [StringLength(80)] public string Codigo { get; set; }="";
 [Required(ErrorMessage="A descrição é obrigatória."),StringLength(2000)] public string Descricao { get; set; }="";
 [Required] public string Status { get; set; }="RASCUNHO";
 [Range(typeof(decimal),"0","9999999999999999",ErrorMessage="O valor não pode ser negativo.")] public decimal? Valor { get; set; }
 [DataType(DataType.Date)] public DateOnly? Data { get; set; }
 [StringLength(2000)] public string? Justificativa { get; set; }
 public string DadosJson { get; set; }="{}";
 public IReadOnlyList<ObraOpcao> Obras { get; set; }=[];
 public string Recurso { get; set; }="";
 public IReadOnlyList<string> StatusPermitidos { get; set; }=[];
 public long? Id { get; set; }
 public ObrasRegistroRequest ToRequest()=>new(ObraId,Codigo,Descricao,Status,Valor,Data,Justificativa,DadosJson);
}
