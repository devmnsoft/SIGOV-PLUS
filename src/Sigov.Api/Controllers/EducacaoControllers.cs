using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Common;
using Sigov.Application.Educacao;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController]
public abstract class EducacaoApiControllerBase : ControllerBase
{
    protected ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<T>.Ok(result.Value!));
        var error = result.Error ?? "Falha na operação.";
        return error.Contains("permiss", StringComparison.OrdinalIgnoreCase) ? StatusCode(StatusCodes.Status403Forbidden, ApiResponse<T>.Fail(error)) : BadRequest(ApiResponse<T>.Fail(error));
    }

    protected ActionResult<ApiResponse<object>> FromResult(Result result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<object>.Ok(new { }));
        var error = result.Error ?? "Falha na operação.";
        return error.Contains("permiss", StringComparison.OrdinalIgnoreCase) ? StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(error)) : BadRequest(ApiResponse<object>.Fail(error));
    }
}

[Route("api/educacao/escolas")]
public sealed class EscolasController : EducacaoApiControllerBase
{
    private readonly IEscolaService _service;
    public EscolasController(IEscolaService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<EscolaResponse>>>> Listar([FromQuery] EscolaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<EscolaResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] EscolaCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] EscolaUpdateRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken ct) => FromResult(await _service.ExcluirAsync(id, ct).ConfigureAwait(false));
}

[Route("api/educacao/anos-letivos")]
public sealed class AnosLetivosController : EducacaoApiControllerBase
{
    private readonly IAnoLetivoService _service;
    public AnosLetivosController(IAnoLetivoService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<AnoLetivoResponse>>>> Listar([FromQuery] EscolaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] AnoLetivoCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/encerrar")] public async Task<ActionResult<ApiResponse<object>>> Encerrar(long id, CancellationToken ct) => FromResult(await _service.EncerrarAsync(id, ct).ConfigureAwait(false));
}

[Route("api/educacao/cursos")]
public sealed class CursosController : EducacaoApiControllerBase
{
    private readonly ICursoService _service;
    public CursosController(ICursoService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<CursoResponse>>>> Listar([FromQuery] EscolaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] CursoCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/series")] public async Task<ActionResult<ApiResponse<long>>> CriarSerie(long id, [FromBody] SerieAnoCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarSerieAsync(id, request, ct).ConfigureAwait(false));
}

[Route("api/educacao/turmas")]
public sealed class TurmasController : EducacaoApiControllerBase
{
    private readonly ITurmaService _service;
    public TurmasController(ITurmaService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<TurmaResponse>>>> Listar([FromQuery] TurmaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<TurmaResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] TurmaCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] TurmaUpdateRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken ct) => FromResult(await _service.ExcluirAsync(id, ct).ConfigureAwait(false));
}

[Route("api/educacao/alunos")]
public sealed class AlunosController : EducacaoApiControllerBase
{
    private readonly IAlunoService _service;
    public AlunosController(IAlunoService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<AlunoResumoResponse>>>> Listar([FromQuery] AlunoFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<AlunoDetalheResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] AlunoCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPut("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Atualizar(long id, [FromBody] AlunoUpdateRequest request, CancellationToken ct) => FromResult(await _service.AtualizarAsync(id, request, ct).ConfigureAwait(false));
    [HttpDelete("{id:long}")] public async Task<ActionResult<ApiResponse<object>>> Excluir(long id, CancellationToken ct) => FromResult(await _service.ExcluirAsync(id, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/responsaveis")] public async Task<ActionResult<ApiResponse<long>>> Responsavel(long id, [FromBody] ResponsavelAlunoRequest request, CancellationToken ct) => FromResult(await _service.AdicionarResponsavelAsync(id, request, ct).ConfigureAwait(false));
}

[Route("api/educacao/matriculas")]
public sealed class MatriculasController : EducacaoApiControllerBase
{
    private readonly IMatriculaService _service;
    public MatriculasController(IMatriculaService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<MatriculaResponse>>>> Listar([FromQuery] MatriculaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpGet("{id:long}")] public async Task<ActionResult<ApiResponse<MatriculaResponse>>> Obter(long id, CancellationToken ct) => FromResult(await _service.ObterAsync(id, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] MatriculaCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/cancelar")] public async Task<ActionResult<ApiResponse<object>>> Cancelar(long id, [FromBody] CancelarMatriculaRequest request, CancellationToken ct) => FromResult(await _service.CancelarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/transferir")] public async Task<ActionResult<ApiResponse<object>>> Transferir(long id, [FromBody] TransferirMatriculaRequest request, CancellationToken ct) => FromResult(await _service.TransferirAsync(id, request, ct).ConfigureAwait(false));
}

[Route("api/educacao/professores")]
public sealed class ProfessoresController : EducacaoApiControllerBase
{
    private readonly IProfessorService _service;
    public ProfessoresController(IProfessorService service) => _service = service;
    [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<ProfessorResponse>>>> Listar([FromQuery] EscolaFiltro filtro, CancellationToken ct) => FromResult(await _service.ListarAsync(filtro, ct).ConfigureAwait(false));
    [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] ProfessorCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpPost("{id:long}/turmas")] public async Task<ActionResult<ApiResponse<long>>> Turma(long id, [FromBody] ProfessorTurmaRequest request, CancellationToken ct) => FromResult(await _service.VincularTurmaAsync(id, request, ct).ConfigureAwait(false));
}

[Route("api/educacao/frequencias")]
public sealed class FrequenciasController : EducacaoApiControllerBase { private readonly IFrequenciaService _service; public FrequenciasController(IFrequenciaService service)=>_service=service; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<FrequenciaResponse>>>> Listar([FromQuery] FrequenciaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] FrequenciaCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false)); }

[Route("api/educacao/avaliacoes")]
public sealed class AvaliacoesController : EducacaoApiControllerBase { private readonly IAvaliacaoService _service; public AvaliacoesController(IAvaliacaoService service)=>_service=service; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<AvaliacaoResponse>>>> Listar([FromQuery] TurmaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] AvaliacaoCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false)); [HttpPost("{id:long}/notas")] public async Task<ActionResult<ApiResponse<long>>> Nota(long id,[FromBody] NotaCreateRequest request,CancellationToken ct)=>FromResult(await _service.RegistrarNotaAsync(id,request,ct).ConfigureAwait(false)); }

[Route("api/educacao/pre-matriculas")]
public sealed class PreMatriculasController : EducacaoApiControllerBase { private readonly IPreMatriculaService _service; public PreMatriculasController(IPreMatriculaService service)=>_service=service; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<PreMatriculaResponse>>>> Listar([FromQuery] PreMatriculaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false)); [HttpPost] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] PreMatriculaCreateRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false)); [HttpPost("{id:long}/converter-matricula")] public async Task<ActionResult<ApiResponse<object>>> Converter(long id,[FromBody] ConverterPreMatriculaRequest request,CancellationToken ct)=>FromResult(await _service.ConverterAsync(id,request,ct).ConfigureAwait(false)); [HttpPost("{id:long}/indeferir")] public async Task<ActionResult<ApiResponse<object>>> Indeferir(long id,CancellationToken ct)=>FromResult(await _service.IndeferirAsync(id,ct).ConfigureAwait(false)); }

[Route("api/educacao/educacenso")]
public sealed class EducacensoController : EducacaoApiControllerBase { private readonly IEducacensoService _service; public EducacensoController(IEducacensoService service)=>_service=service; [HttpGet] public async Task<ActionResult<ApiResponse<PagedResult<EducacensoRegistroResponse>>>> Listar([FromQuery] EscolaFiltro filtro,CancellationToken ct)=>FromResult(await _service.ListarAsync(filtro,ct).ConfigureAwait(false)); [HttpPost("registros")] public async Task<ActionResult<ApiResponse<long>>> Criar([FromBody] EducacensoRegistroRequest request,CancellationToken ct)=>FromResult(await _service.CriarAsync(request,ct).ConfigureAwait(false)); [HttpPost("{id:long}/validar-dev")] public async Task<ActionResult<ApiResponse<object>>> Validar(long id,CancellationToken ct)=>FromResult(await _service.ValidarDevAsync(id,ct).ConfigureAwait(false)); }

[Route("api/educacao/dashboard")]
public sealed class EducacaoDashboardController : EducacaoApiControllerBase { private readonly IEducacaoDashboardService _service; public EducacaoDashboardController(IEducacaoDashboardService service)=>_service=service; [HttpGet] public async Task<ActionResult<ApiResponse<EducacaoDashboardResponse>>> Obter(CancellationToken ct)=>FromResult(await _service.ObterAsync(ct).ConfigureAwait(false)); }

[Route("api/educacao/boletins")]
public sealed class BoletinsController : EducacaoApiControllerBase
{
    private readonly IBoletimService _service;
    public BoletinsController(IBoletimService service) => _service = service;
    [HttpGet("{alunoId:long}")]
    public async Task<ActionResult<ApiResponse<BoletimResponse>>> Obter(long alunoId, CancellationToken ct) => FromResult(await _service.ObterAsync(alunoId, ct).ConfigureAwait(false));
}

[Route("api/educacao/export")]
public sealed class EducacaoExportacaoController : EducacaoApiControllerBase { private readonly IEducacaoExportacaoService _service; public EducacaoExportacaoController(IEducacaoExportacaoService service)=>_service=service; [HttpGet("{recurso}.{formato}")] public async Task<IActionResult> Exportar(string recurso,string formato,CancellationToken ct){var r=await _service.ExportarAsync(recurso,formato,ct).ConfigureAwait(false); if(r.IsFailure)return BadRequest(ApiResponse<object>.Fail(r.Error??"Falha na exportação.")); return File(r.Value??Array.Empty<byte>(), formato.Equals("json",StringComparison.OrdinalIgnoreCase)?"application/json":"text/csv", $"{recurso}.{formato}");} }
