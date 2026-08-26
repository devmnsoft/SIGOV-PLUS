using Sigov.Application.Fiscalizacao;

namespace Sigov.Web.Models.Fiscalizacao;
public sealed record FiscalizacaoListaViewModel(string Titulo,string Recurso,IReadOnlyList<FiscalizacaoLinha> Itens,FiscalizacaoFiltro Filtro);
public sealed record FiscalizacaoOrdemFormViewModel(long? Id,OrdemFiscalizacaoRequest Ordem,IReadOnlyList<FiscalizacaoOpcao> Equipes,IReadOnlyList<FiscalizacaoOpcao> Registros);
