using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Application.Educacao;
using Sigov.Application.Rh;

namespace Sigov.Api.Controllers;

[ApiController]
public sealed class OperationalImportsController : ControllerBase
{
    private readonly IOperationalImportStore _store;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IAlunoService _alunos;
    private readonly IMatriculaService _matriculas;
    private readonly IFrequenciaService _frequencias;
    private readonly IAvaliacaoService _avaliacoes;
    private readonly IRhService _rh;

    public OperationalImportsController(IOperationalImportStore store, ICurrentTenant tenant, ICurrentUser user, IAlunoService alunos, IMatriculaService matriculas, IFrequenciaService frequencias, IAvaliacaoService avaliacoes, IRhService rh)
    { _store = store; _tenant = tenant; _user = user; _alunos = alunos; _matriculas = matriculas; _frequencias = frequencias; _avaliacoes = avaliacoes; _rh = rh; }

    [HttpPost("api/educacao/importacoes/{resource:regex(^alunos|matriculas|frequencias|notas$)}/preview")]
    public ActionResult<ApiResponse<OperationalImportPreview>> PreviewEducation(string resource, OperationalCsvImportRequest request) => Preview("EDUCACAO", resource, request);

    [HttpPost("api/rh/importacoes/{resource:regex(^servidores|vinculos|lancamentos|ponto$)}/preview")]
    public ActionResult<ApiResponse<OperationalImportPreview>> PreviewHumanResources(string resource, OperationalCsvImportRequest request) => Preview("RH", resource, request);

    [HttpPost("api/educacao/importacoes/{resource:regex(^alunos|matriculas|frequencias|notas$)}/confirmar")]
    public Task<ActionResult<ApiResponse<OperationalImportConfirmation>>> ConfirmEducation(string resource, OperationalCsvImportRequest request, CancellationToken ct) => Confirm("EDUCACAO", resource, request, ct);

    [HttpPost("api/rh/importacoes/{resource:regex(^servidores|vinculos|lancamentos|ponto$)}/confirmar")]
    public Task<ActionResult<ApiResponse<OperationalImportConfirmation>>> ConfirmHumanResources(string resource, OperationalCsvImportRequest request, CancellationToken ct) => Confirm("RH", resource, request, ct);

    private ActionResult<ApiResponse<OperationalImportPreview>> Preview(string module, string resource, OperationalCsvImportRequest request)
    {
        if ((_tenant.TenantId ?? 0) <= 0) return BadRequest(ApiResponse<OperationalImportPreview>.Fail("Tenant obrigatório."));
        var preview = Parse(module, resource, request);
        var masked = preview with { Rows = preview.Rows.Select(x => x with { Values = Mask(x.Values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase)) }).ToArray() };
        return Ok(ApiResponse<OperationalImportPreview>.Ok(masked));
    }

    private async Task<ActionResult<ApiResponse<OperationalImportConfirmation>>> Confirm(string module, string resource, OperationalCsvImportRequest request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? 0;
        if (tenantId <= 0) return BadRequest(ApiResponse<OperationalImportConfirmation>.Fail("Tenant obrigatório."));
        var preview = Parse(module, resource, request);
        var issues = preview.Rows.SelectMany(x => x.Issues).ToList();
        var persisted = 0;
        foreach (var row in preview.Rows.Where(x => x.Valid))
        {
            try
            {
                var result = await Persist(module, resource, row.Values, ct).ConfigureAwait(false);
                if (result.IsSuccess) persisted++;
                else issues.Add(new OperationalImportIssue(row.Line, "registro", result.Error ?? "Registro rejeitado.", "ERRO"));
            }
            catch (FormatException)
            {
                issues.Add(new OperationalImportIssue(row.Line, "formato", "Número ou data em formato inválido.", "ERRO"));
            }
        }
        var rejected = preview.Total - persisted;
        var correlationId = HttpContext.TraceIdentifier;
        var reportId = await _store.SaveReportAsync(tenantId, module, resource, preview.Total, persisted, rejected,
            new { issues, sample = preview.Rows.Take(25).Select(x => new { x.Line, x.Valid }) }, _user.UsuarioId, correlationId, ct).ConfigureAwait(false);
        return Ok(ApiResponse<OperationalImportConfirmation>.Ok(new(reportId, persisted, rejected, issues)));
    }

    private async Task<Sigov.Domain.Common.Result> Persist(string module, string resource, IReadOnlyDictionary<string, string> v, CancellationToken ct)
    {
        if (module == "RH")
        {
            var target = resource switch { "lancamentos" => "folha-lancamentos", "ponto" => "pontos", _ => resource };
            var data = v.ToDictionary(x => x.Key, x => (object?)x.Value, StringComparer.OrdinalIgnoreCase);
            var result = await _rh.CriarAsync(target, new RhRegistroCreateRequest(data), ct).ConfigureAwait(false);
            return result.IsSuccess ? Sigov.Domain.Common.Result.Success() : Sigov.Domain.Common.Result.Failure(result.Error ?? "Falha no RH.");
        }
        if (resource == "alunos") return ToResult(await _alunos.CriarAsync(new(Long(v, "pessoa_id"), Required(v, "codigo_aluno"), Get(v, "nis"), Get(v, "cartao_sus")), ct).ConfigureAwait(false));
        if (resource == "matriculas") return ToResult(await _matriculas.CriarAsync(new(Long(v,"aluno_id"), Long(v,"escola_id"), Long(v,"ano_letivo_id"), Long(v,"turma_id"), Get(v,"numero_matricula")), ct).ConfigureAwait(false));
        if (resource == "frequencias") return ToResult(await _frequencias.CriarAsync(new(Long(v,"turma_id"), Long(v,"aluno_id"), NullableLong(v,"professor_id"), DateOnly.Parse(Required(v,"data_aula"), CultureInfo.InvariantCulture), Get(v,"componente_curricular"), !Required(v,"status").Equals("FALTA",StringComparison.OrdinalIgnoreCase), Get(v,"justificativa"), Required(v,"status")), ct).ConfigureAwait(false));
        var grade = decimal.Parse(Required(v,"valor"), CultureInfo.InvariantCulture);
        return ToResult(await _avaliacoes.RegistrarNotaAsync(Long(v,"avaliacao_id"), new(Long(v,"aluno_id"), grade, Get(v,"observacao")), ct).ConfigureAwait(false));
    }

    private static Sigov.Domain.Common.Result ToResult<T>(Sigov.Domain.Common.Result<T> result) => result.IsSuccess ? Sigov.Domain.Common.Result.Success() : Sigov.Domain.Common.Result.Failure(result.Error ?? "Registro rejeitado.");

    private static OperationalImportPreview Parse(string module, string resource, OperationalCsvImportRequest request)
    {
        var lines = (request.Csv ?? string.Empty).Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 2) return new(module, resource, 0, 0, 0, Array.Empty<OperationalImportRow>());
        var headers = lines[0].Split(request.Delimiter).Select(Normalize).ToArray();
        var required = RequiredFields(module, resource);
        var rows = new List<OperationalImportRow>();
        for (var i = 1; i < lines.Length; i++)
        {
            var cells = lines[i].Split(request.Delimiter);
            var values = headers.Select((h, n) => (h, value: n < cells.Length ? cells[n].Trim() : string.Empty)).ToDictionary(x => x.h, x => x.value, StringComparer.OrdinalIgnoreCase);
            var errors = required.Where(f => !values.TryGetValue(f, out var value) || string.IsNullOrWhiteSpace(value)).Select(f => new OperationalImportIssue(i + 1, f, "Campo obrigatório não informado.", "ERRO")).ToArray();
            rows.Add(new(i + 1, values, errors.Length == 0, errors));
        }
        return new(module, resource, rows.Count, rows.Count(x => x.Valid), rows.Count(x => !x.Valid), rows);
    }

    private static string[] RequiredFields(string module, string resource) => (module, resource) switch
    {
        ("EDUCACAO", "alunos") => ["pessoa_id", "codigo_aluno"],
        ("EDUCACAO", "matriculas") => ["aluno_id", "escola_id", "ano_letivo_id", "turma_id"],
        ("EDUCACAO", "frequencias") => ["turma_id", "aluno_id", "data_aula", "status"],
        ("EDUCACAO", "notas") => ["avaliacao_id", "aluno_id", "valor"],
        ("RH", "servidores") => ["matricula", "nome", "cpf"],
        ("RH", "vinculos") => ["servidorid", "cargoid", "lotacaoid", "tipo", "dataadmissao"],
        ("RH", "lancamentos") => ["folhaid", "servidorid", "eventoid", "valor"],
        _ => ["servidorid", "datahora", "tipo"]
    };
    private static IReadOnlyDictionary<string,string> Mask(Dictionary<string,string> values) => values.ToDictionary(x => x.Key, x => x.Key.Contains("cpf",StringComparison.OrdinalIgnoreCase) || x.Key.Contains("documento",StringComparison.OrdinalIgnoreCase) ? (x.Value.Length > 4 ? $"***{x.Value[^4..]}" : "***") : x.Value, StringComparer.OrdinalIgnoreCase);
    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace(' ', '_');
    private static string Required(IReadOnlyDictionary<string,string> v,string key) => v.TryGetValue(key,out var value) ? value : string.Empty;
    private static string? Get(IReadOnlyDictionary<string,string> v,string key) => v.TryGetValue(key,out var value) && value.Length > 0 ? value : null;
    private static long Long(IReadOnlyDictionary<string,string> v,string key) => long.Parse(Required(v,key), CultureInfo.InvariantCulture);
    private static long? NullableLong(IReadOnlyDictionary<string,string> v,string key) => long.TryParse(Get(v,key), CultureInfo.InvariantCulture, out var value) ? value : null;
}
