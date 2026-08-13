using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;
using Sigov.Application.Educacao.Bloco3;

namespace Sigov.Api.Controllers;

[Route("api/educacao/secretaria")]
public sealed class EducacaoSecretariaController : EducacaoApiControllerBase
{
    private readonly IEducacaoSecretariaService _service;
    public EducacaoSecretariaController(IEducacaoSecretariaService service) => _service=service;
    [HttpGet("dashboard")] public async Task<IActionResult> Dashboard(CancellationToken ct) => Ok(ApiResponse<object>.Ok(new { documentos=await Dados<EducacaoDocumentoEscolarDto>("documento",ct), solicitacoes=await Dados<EducacaoSolicitacaoEscolarDto>("solicitacao",ct), pendencias=await Dados<EducacaoPendenciaDocumentalDto>("pendencia",ct), diariosPendentes=await Dados<EducacaoDiarioPendenciaDto>("diario-pendencia",ct) }));
    [HttpGet("documentos")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoDocumentoEscolarDto>>>> Documentos([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoDocumentoEscolarDto>("documento",f,ct));
    [HttpPost("declaracao-matricula")] public async Task<ActionResult<ApiResponse<long>>> DeclaracaoMatricula(EducacaoEmitirDeclaracaoMatriculaRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("documento",r,ct));
    [HttpPost("declaracao-frequencia")] public async Task<ActionResult<ApiResponse<long>>> DeclaracaoFrequencia(EducacaoEmitirDeclaracaoFrequenciaRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("documento-frequencia",r,ct));
    [HttpGet("solicitacoes")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoSolicitacaoEscolarDto>>>> Solicitacoes([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoSolicitacaoEscolarDto>("solicitacao",f,ct));
    [HttpPost("solicitacoes")] public async Task<ActionResult<ApiResponse<long>>> Solicitar(EducacaoCriarSolicitacaoEscolarRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("solicitacao",r,ct));
    [HttpPost("solicitacoes/{id:long}/deferir")] public Task<ActionResult<ApiResponse<object>>> Deferir(long id,EducacaoDecidirSolicitacaoEscolarRequest r,CancellationToken ct)=>Decidir("solicitacao",id,"DEFERIDA",r.Justificativa,ct);
    [HttpPost("solicitacoes/{id:long}/indeferir")] public Task<ActionResult<ApiResponse<object>>> Indeferir(long id,EducacaoDecidirSolicitacaoEscolarRequest r,CancellationToken ct)=>Decidir("solicitacao",id,"INDEFERIDA",r.Justificativa,ct);
    [HttpPost("solicitacoes/{id:long}/concluir")] public Task<ActionResult<ApiResponse<object>>> ConcluirSolicitacao(long id,EducacaoDecidirSolicitacaoEscolarRequest r,CancellationToken ct)=>Decidir("solicitacao",id,"CONCLUIDA",r.Justificativa,ct);
    [HttpGet("pendencias-documentais")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoPendenciaDocumentalDto>>>> Pendencias([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoPendenciaDocumentalDto>("pendencia",f,ct));
    [HttpPost("pendencias-documentais")] public async Task<ActionResult<ApiResponse<long>>> CriarPendencia(EducacaoCriarPendenciaDocumentalRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("pendencia",r,ct));
    [HttpPost("pendencias-documentais/{id:long}/resolver")] public Task<ActionResult<ApiResponse<object>>> Resolver(long id,EducacaoDecidirSolicitacaoEscolarRequest r,CancellationToken ct)=>Decidir("pendencia",id,"RESOLVIDA",r.Justificativa,ct);
    [HttpGet("transferencias")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoTransferenciaDto>>>> Transferencias([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoTransferenciaDto>("transferencia",f,ct));
    [HttpPost("transferencias")] public async Task<ActionResult<ApiResponse<long>>> Transferir(EducacaoSolicitarTransferenciaRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("transferencia",r,ct));
    [HttpPost("transferencias/{id:long}/aprovar")] public Task<ActionResult<ApiResponse<object>>> Aprovar(long id,EducacaoDecidirTransferenciaRequest r,CancellationToken ct)=>Decidir("transferencia",id,"APROVADA",r.Justificativa,ct);
    [HttpPost("transferencias/{id:long}/reprovar")] public Task<ActionResult<ApiResponse<object>>> Reprovar(long id,EducacaoDecidirTransferenciaRequest r,CancellationToken ct)=>Decidir("transferencia",id,"REPROVADA",r.Justificativa,ct);
    [HttpPost("transferencias/{id:long}/concluir")] public Task<ActionResult<ApiResponse<object>>> ConcluirTransferencia(long id,EducacaoDecidirTransferenciaRequest r,CancellationToken ct)=>Decidir("transferencia",id,"CONCLUIDA",r.Justificativa,ct);
    [HttpGet("ocorrencias")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoOcorrenciaEscolarDto>>>> Ocorrencias([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoOcorrenciaEscolarDto>("ocorrencia",f,ct));
    [HttpPost("ocorrencias")] public async Task<ActionResult<ApiResponse<long>>> Ocorrencia(EducacaoCriarOcorrenciaEscolarRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("ocorrencia",r,ct));
    [HttpGet("relatorios/resumo")] public Task<IActionResult> Resumo(CancellationToken ct)=>Dashboard(ct);
    private async Task<IReadOnlyCollection<T>> Dados<T>(string recurso,CancellationToken ct){var r=await _service.ListarAsync<T>(recurso,new(),ct);return r.IsSuccess?r.Value!:Array.Empty<T>();}
    private async Task<ActionResult<ApiResponse<object>>> Decidir(string recurso,long id,string status,string justificativa,CancellationToken ct)=>FromResult(await _service.DecidirAsync(recurso,id,status,justificativa,ct));
}

[Route("api/educacao/diario-classe")]
public sealed class EducacaoDiarioClasseController : EducacaoApiControllerBase
{
    private readonly IEducacaoDiarioClasseService _service; public EducacaoDiarioClasseController(IEducacaoDiarioClasseService service)=>_service=service;
    [HttpGet] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoDiarioClasseDto>>>> Listar([FromQuery] EducacaoBloco3Filtro f,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoDiarioClasseDto>("diario",f,ct));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar(EducacaoCriarDiarioClasseRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("diario",r,ct));
    [HttpPost("{id:long}/aulas")] public async Task<ActionResult<ApiResponse<long>>> Aula(long id,EducacaoCriarDiarioAulaRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("aula",new{DiarioId=id,r.DataAula,r.CargaHoraria,r.Observacoes},ct));
    [HttpPost("{id:long}/conteudo")] public async Task<ActionResult<ApiResponse<long>>> Conteudo(long id,EducacaoDiarioConteudoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("conteudo",new{DiarioId=id,r.AulaId,r.Conteudo,r.Observacoes},ct));
    [HttpPost("{id:long}/frequencia")] public async Task<ActionResult<ApiResponse<long>>> Frequencia(long id,EducacaoDiarioFrequenciaRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("frequencia",new{DiarioId=id,r.AulaId,r.Alunos},ct));
    [HttpPost("{id:long}/avaliacoes")] public async Task<ActionResult<ApiResponse<long>>> Avaliacao(long id,EducacaoDiarioAvaliacaoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("avaliacao",new{DiarioId=id,r.AulaId,r.Titulo,r.ValorMaximo,r.Peso},ct));
    [HttpPost("{id:long}/reposicao")] public async Task<ActionResult<ApiResponse<long>>> Reposicao(long id,EducacaoDiarioReposicaoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("reposicao",new{DiarioId=id,r.AulaId,r.DataReposicao,r.Justificativa},ct));
    [HttpPost("{id:long}/fechar-periodo")] public async Task<ActionResult<ApiResponse<object>>> Fechar(long id,EducacaoDiarioFechamentoRequest r,CancellationToken ct)=>FromResult(await _service.DecidirAsync("diario",id,"FECHADO",r.Observacao,ct));
    [HttpPost("{id:long}/reabrir")] public async Task<ActionResult<ApiResponse<object>>> Reabrir(long id,EducacaoDiarioReaberturaRequest r,CancellationToken ct)=>FromResult(await _service.DecidirAsync("diario",id,"REABERTO",r.Justificativa,ct));
    [HttpGet("pendencias")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoDiarioPendenciaDto>>>> Pendencias(CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoDiarioPendenciaDto>("diario-pendencia",new(),ct));
    [HttpGet("relatorios/resumo")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoDiarioClasseDto>>>> Resumo(CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoDiarioClasseDto>("diario",new(),ct));
}

[Route("api/educacao/portal")]
public sealed class EducacaoPortalController : EducacaoApiControllerBase
{
    private readonly IEducacaoPortalService _service; public EducacaoPortalController(IEducacaoPortalService service)=>_service=service;
    [HttpGet("resumo")] public async Task<IActionResult> Resumo(CancellationToken ct)=>Ok(ApiResponse<object>.Ok(new{solicitacoes=await Dados<EducacaoPortalSolicitacaoDto>("portal-solicitacao",ct),mensagens=await Dados<EducacaoPortalMensagemDto>("portal-mensagem",ct)}));
    [HttpGet("alunos/{alunoId:long}/ocorrencias")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoOcorrenciaEscolarDto>>>> Ocorrencias(long alunoId,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoOcorrenciaEscolarDto>("portal-ocorrencia",new(AlunoId:alunoId),ct));
    [HttpGet("alunos/{alunoId:long}/comunicados")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoPortalComunicadoDto>>>> Comunicados(long alunoId,CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoPortalComunicadoDto>("portal-comunicado",new(AlunoId:alunoId),ct));
    [HttpGet("solicitacoes")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoPortalSolicitacaoDto>>>> Solicitacoes(CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoPortalSolicitacaoDto>("portal-solicitacao",new(),ct));
    [HttpPost("solicitacoes")] public async Task<ActionResult<ApiResponse<long>>> Solicitar(EducacaoPortalCriarSolicitacaoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("portal-solicitacao",r,ct));
    [HttpGet("mensagens")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoPortalMensagemDto>>>> Mensagens(CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoPortalMensagemDto>("portal-mensagem",new(),ct));
    [HttpGet("admin/vinculos")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<EducacaoPortalVinculoDto>>>> Vinculos(CancellationToken ct)=>FromResult(await _service.ListarAsync<EducacaoPortalVinculoDto>("portal-vinculo",new(),ct));
    [HttpPost("admin/vinculos")] public async Task<ActionResult<ApiResponse<long>>> Vinculo(EducacaoPortalCriarVinculoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("portal-vinculo",r,ct));
    [HttpPost("admin/solicitacoes/{id:long}/responder")] public async Task<ActionResult<ApiResponse<object>>> Responder(long id,EducacaoPortalResponderSolicitacaoRequest r,CancellationToken ct)=>FromResult(await _service.DecidirAsync("portal-solicitacao",id,r.Status,r.Resposta,ct));
    [HttpPost("admin/comunicados")] public async Task<ActionResult<ApiResponse<long>>> Comunicado(EducacaoPortalCriarComunicadoRequest r,CancellationToken ct)=>FromResult(await _service.CriarAsync("portal-comunicado",r,ct));
    private async Task<IReadOnlyCollection<T>> Dados<T>(string recurso,CancellationToken ct){var r=await _service.ListarAsync<T>(recurso,new(),ct);return r.IsSuccess?r.Value!:Array.Empty<T>();}
}
