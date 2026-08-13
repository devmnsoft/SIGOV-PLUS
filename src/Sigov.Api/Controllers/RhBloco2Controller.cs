using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Common;
using Sigov.Application.Rh;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/rh")]
public sealed class RhBloco2Controller : ProcessosControllerBase
{
    private readonly IRhService _service;
    public RhBloco2Controller(IRhService service) => _service = service;
    private static Dictionary<string, object?> Dados(object value) => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(System.Text.Json.JsonSerializer.Serialize(value))!;
    private Task<Sigov.Domain.Common.Result<long>> Criar(string recurso, object value, CancellationToken ct) => _service.CriarAsync(recurso, new RhRegistroCreateRequest(Dados(value)), ct);
    private async Task<Sigov.Domain.Common.Result> Status(string recurso, long id, string status, object? extra, CancellationToken ct)
    {
        var current = await _service.ObterAsync(recurso, id, ct); if (current.IsFailure) return Sigov.Domain.Common.Result.Failure(current.Error ?? "Registro não encontrado.");
        var dados = current.Value!.Dados; dados["status"] = status; dados["transicaoEm"] = DateTimeOffset.UtcNow; if (extra is not null) dados["decisao"] = Dados(extra);
        return await _service.AtualizarAsync(recurso, id, new RhRegistroUpdateRequest(dados), ct);
    }

    [HttpGet("ponto/dashboard")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> PontoDashboard(CancellationToken ct) => FromResult(await _service.ListarAsync("ponto-apuracoes", new RhFiltro(1,20), ct));
    [HttpGet("ponto/jornadas")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Jornadas([FromQuery] RhFiltro f, CancellationToken ct) => FromResult(await _service.ListarAsync("ponto-jornadas",f,ct));
    [HttpPost("ponto/jornadas")] public async Task<ActionResult<ApiResponse<long>>> CriarJornada(RhPontoCriarJornadaRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-jornadas",r,ct));
    [HttpPut("ponto/jornadas/{id:long}")] public async Task<ActionResult<ApiResponse<object>>> AtualizarJornada(long id,RhPontoCriarJornadaRequest r,CancellationToken ct)=>FromResult(await _service.AtualizarAsync("ponto-jornadas",id,new(Dados(r)),ct));
    [HttpGet("ponto/escalas")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Escalas([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-escalas",f,ct));
    [HttpPost("ponto/escalas")] public async Task<ActionResult<ApiResponse<long>>> CriarEscala(RhPontoCriarEscalaRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-escalas",r,ct));
    [HttpPost("ponto/escalas/{id:long}/inativar")] public async Task<ActionResult<ApiResponse<object>>> InativarEscala(long id,CancellationToken ct)=>FromResult(await Status("ponto-escalas",id,"INATIVA",null,ct));
    [HttpGet("ponto/registros")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Registros([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-registros",f,ct));
    [HttpPost("ponto/registros")] public async Task<ActionResult<ApiResponse<long>>> Registrar(RhPontoRegistrarBatidaRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-registros",r,ct));
    [HttpPost("ponto/registros/{id:long}/ajustar")] public async Task<ActionResult<ApiResponse<object>>> Ajustar(long id,RhPontoRegistrarBatidaRequest r,CancellationToken ct)=>FromResult(string.IsNullOrWhiteSpace(r.Justificativa)?Sigov.Domain.Common.Result.Failure("Justificativa obrigatória para ajuste manual."):await _service.AtualizarAsync("ponto-registros",id,new(Dados(r with { Origem="AJUSTADO" })),ct));
    [HttpGet("ponto/justificativas")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Justificativas([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-justificativas",f,ct));
    [HttpPost("ponto/justificativas")] public async Task<ActionResult<ApiResponse<long>>> Justificar(RhPontoCriarJustificativaRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-justificativas",r,ct));
    [HttpPost("ponto/justificativas/{id:long}/{decisao:regex(aprovar|reprovar)}")] public async Task<ActionResult<ApiResponse<object>>> DecidirJustificativa(long id,string decisao,CancellationToken ct)=>FromResult(await Status("ponto-justificativas",id,decisao=="aprovar"?"APROVADA":"REPROVADA",null,ct));
    [HttpPost("ponto/apurar")] public async Task<ActionResult<ApiResponse<long>>> Apurar(RhPontoApuracaoRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-apuracoes",new { r.ServidorId,r.PeriodoInicio,r.PeriodoFim,status="APURADA",calculo="HORAS_ATRASOS_FALTAS_EXTRAS_SALDO"},ct));
    [HttpGet("ponto/apuracoes")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Apuracoes([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-apuracoes",f,ct));
    [HttpGet("ponto/apuracoes/{id:long}")] public async Task<ActionResult<ApiResponse<RhRegistroResponse>>> Apuracao(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync("ponto-apuracoes",id,ct));
    [HttpPost("ponto/apuracoes/{id:long}/homologar")] public async Task<ActionResult<ApiResponse<object>>> Homologar(long id,RhPontoHomologacaoRequest r,CancellationToken ct)=>FromResult(await Status("ponto-apuracoes",id,"HOMOLOGADA",r,ct));
    [HttpPost("ponto/apuracoes/{id:long}/integrar-folha")] public async Task<ActionResult<ApiResponse<long>>> IntegrarFolha(long id,RhPontoIntegracaoFolhaRequest r,CancellationToken ct)=>FromResult(await Criar("ponto-integracoes-folha",new { apuracaoId=id,r.FolhaId,status="PENDENTE",origem="PONTO"},ct));
    [HttpGet("ponto/espelho")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Espelho([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-registros",f,ct));
    [HttpGet("ponto/relatorios/resumo")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> ResumoPonto([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ponto-apuracoes",f,ct));
    [HttpGet("ponto/relatorios/exportar-csv")] public Task<IActionResult> ExportarPonto(CancellationToken ct)=>Exportar("ponto-apuracoes","ponto-resumo",ct);

    [HttpGet("ferias/dashboard")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> FeriasDashboard(CancellationToken ct)=>FromResult(await _service.ListarAsync("ferias-programacoes",new RhFiltro(1,20),ct));
    [HttpGet("ferias/periodos-aquisitivos")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Periodos([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ferias-periodos",f,ct));
    [HttpPost("ferias/periodos-aquisitivos")] public async Task<ActionResult<ApiResponse<long>>> CriarPeriodo(RhFeriasPeriodoAquisitivoDto r,CancellationToken ct)=>FromResult(await Criar("ferias-periodos",r,ct));
    [HttpGet("ferias/programacoes")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Programacoes([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("ferias-programacoes",f,ct));
    [HttpPost("ferias/programacoes")] public async Task<ActionResult<ApiResponse<long>>> Programar(RhFeriasSolicitacaoRequest r,CancellationToken ct)=>FromResult(await Criar("ferias-programacoes",r,ct));
    [HttpPost("ferias/programacoes/{id:long}/{acao:regex(solicitar|aprovar|reprovar)}")] public async Task<ActionResult<ApiResponse<object>>> FluxoFerias(long id,string acao,RhFeriasAprovacaoRequest r,CancellationToken ct)=>FromResult(await Status("ferias-programacoes",id,acao.ToUpperInvariant() switch {"SOLICITAR"=>"SOLICITADA", "APROVAR"=>"APROVADA",_=>"REPROVADA"},r,ct));
    [HttpPost("ferias/programacoes/{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> CancelarFerias(long id,RhFeriasCancelamentoRequest r,CancellationToken ct)=>FromResult(string.IsNullOrWhiteSpace(r.Justificativa)?Sigov.Domain.Common.Result.Failure("Justificativa obrigatória."):await Status("ferias-programacoes",id,"CANCELADA",r,ct));
    [HttpGet("ferias/servidores/{servidorId:long}/historico")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> HistoricoFerias(long servidorId,CancellationToken ct)=>FromResult(await _service.ListarAsync("ferias-historicos",new RhFiltro(1,100,servidorId.ToString()),ct));

    [HttpGet("afastamentos/tipos")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Tipos([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("afastamento-tipos",f,ct));
    [HttpPost("afastamentos/tipos")] public async Task<ActionResult<ApiResponse<long>>> CriarTipo(RhRegistroCreateRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("afastamento-tipos",r,ct));
    [HttpGet("afastamentos")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Afastamentos([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("afastamentos",f,ct));
    [HttpPost("afastamentos")] public async Task<ActionResult<ApiResponse<long>>> CriarAfastamento(RhCriarAfastamentoRequest r,CancellationToken ct)=>FromResult(await Criar("afastamentos",r,ct));
    [HttpGet("afastamentos/{id:long}")] public async Task<ActionResult<ApiResponse<RhRegistroResponse>>> Afastamento(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync("afastamentos",id,ct));
    [HttpPost("afastamentos/{id:long}/{acao:regex(aprovar|reprovar|cancelar)}")] public async Task<ActionResult<ApiResponse<object>>> FluxoAfastamento(long id,string acao,RhAfastamentoAprovacaoRequest r,CancellationToken ct)=>FromResult(await Status("afastamentos",id,acao.ToUpperInvariant() switch {"APROVAR"=>"APROVADO","REPROVAR"=>"REPROVADO",_=>"CANCELADO"},r,ct));
    [HttpPost("afastamentos/{id:long}/encerrar")] public async Task<ActionResult<ApiResponse<object>>> Encerrar(long id,RhAfastamentoEncerramentoRequest r,CancellationToken ct)=>FromResult(await Status("afastamentos",id,"ENCERRADO",r,ct));
    [HttpGet("afastamentos/servidores/{servidorId:long}/historico")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> HistoricoAfastamento(long servidorId,CancellationToken ct)=>FromResult(await _service.ListarAsync("afastamento-historicos",new RhFiltro(1,100,servidorId.ToString()),ct));
    [HttpGet("ferias-afastamentos/relatorios/resumo")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> ResumoFerias(CancellationToken ct)=>FromResult(await _service.ListarAsync("ferias-programacoes",new RhFiltro(1,100),ct));
    [HttpGet("ferias-afastamentos/relatorios/exportar-csv")] public Task<IActionResult> ExportarAusencias(CancellationToken ct)=>Exportar("ferias-programacoes","ferias-afastamentos",ct);

    [HttpGet("portal/resumo")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> PortalResumo(CancellationToken ct)=>FromResult(await _service.ListarAsync("portal-solicitacoes",new RhFiltro(1,10),ct));
    [HttpGet("portal/solicitacoes")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Solicitacoes([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("portal-solicitacoes",f,ct));
    [HttpPost("portal/solicitacoes")] public async Task<ActionResult<ApiResponse<long>>> Solicitar(RhPortalCriarSolicitacaoRequest r,CancellationToken ct)=>FromResult(await Criar("portal-solicitacoes",new {r.Tipo,r.Descricao,status="ABERTA"},ct));
    [HttpGet("portal/solicitacoes/{id:long}")] public async Task<ActionResult<ApiResponse<RhRegistroResponse>>> Solicitacao(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync("portal-solicitacoes",id,ct));
    [HttpPost("portal/atualizacao-cadastral")] public async Task<ActionResult<ApiResponse<long>>> Atualizacao(RhPortalAtualizacaoCadastralRequest r,CancellationToken ct)=>FromResult(await Criar("portal-atualizacoes",new {r.Dados,status="EM_ANALISE"},ct));
    [HttpGet("portal/mensagens")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Mensagens([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("portal-mensagens",f,ct));
    [HttpGet("portal/{secao:regex(meus-dados|contracheques|ferias|afastamentos|ponto)}")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> MinhaSecao(string secao,CancellationToken ct)=>FromResult(await _service.ListarAsync(secao switch {"contracheques"=>"folha-lancamentos","ferias"=>"ferias-programacoes","afastamentos"=>"afastamentos","ponto"=>"ponto-registros",_=>"portal-usuarios"},new RhFiltro(1,24),ct));
    [HttpGet("portal/contracheques/{id:long}")] public async Task<ActionResult<ApiResponse<RhRegistroResponse>>> Contracheque(long id,CancellationToken ct)=>FromResult(await _service.ObterAsync("folha-lancamentos",id,ct));
    [HttpGet("portal/admin/solicitacoes")] public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> AdminSolicitacoes([FromQuery] RhFiltro f,CancellationToken ct)=>FromResult(await _service.ListarAsync("portal-solicitacoes",f,ct));
    [HttpPost("portal/admin/solicitacoes/{id:long}/{acao:regex(aprovar|reprovar)}")] public async Task<ActionResult<ApiResponse<object>>> DecidirSolicitacao(long id,string acao,RhPortalRespostaRequest r,CancellationToken ct)=>FromResult(await Status("portal-solicitacoes",id,acao=="aprovar"?"APROVADA":"REPROVADA",r,ct));
    [HttpPost("portal/admin/solicitacoes/{id:long}/responder")] public async Task<ActionResult<ApiResponse<object>>> Responder(long id,RhPortalRespostaRequest r,CancellationToken ct)=>FromResult(await Status("portal-solicitacoes",id,"CONCLUIDA",r,ct));

    private async Task<IActionResult> Exportar(string recurso,string nome,CancellationToken ct)
    {
        var result=await _service.ExportarAsync(recurso,"csv",ct);
        return result.IsFailure ? BadRequest(ApiResponse<object>.Fail(result.Error ?? "Falha na exportação.")) : File(result.Value ?? Array.Empty<byte>(),"text/csv",$"{nome}.csv");
    }
}
