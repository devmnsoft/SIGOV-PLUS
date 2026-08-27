using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Sigov.Application.Sst;

namespace Sigov.Web.Models.Sst;
public sealed class SstAsoForm
{
 public long? Id {get;set;}
 [Range(1,long.MaxValue,ErrorMessage="Selecione o servidor.")] public long ServidorId {get;set;}
 [Required] public string Tipo {get;set;}="periodico";
 [Required,DataType(DataType.Date),Display(Name="Data do ASO")] public DateOnly DataAso {get;set;}=DateOnly.FromDateTime(DateTime.Today);
 [Required,StringLength(160)] public string Medico {get;set;}=string.Empty;
 [Required] public string Resultado {get;set;}="pendente";
 [StringLength(1000)] public string? Restricao {get;set;}
 [DataType(DataType.Date),Display(Name="Validade")] public DateOnly? Validade {get;set;}
 public IReadOnlyList<SelectListItem> Servidores {get;set;}=[];
}
public sealed record SstAsoIndex(IReadOnlyList<SstAso> Itens);
public sealed record SstModulePage(string Titulo,string Descricao,string Badge);
