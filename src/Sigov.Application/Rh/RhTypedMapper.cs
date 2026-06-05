using System.Globalization;
using System.Text.Json;
using Sigov.Application.Common;
using Sigov.Application.Rh.Dto;

namespace Sigov.Application.Rh;

public static class RhTypedMapper
{
    public static RhFiltro ToFiltro(ServidorFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);
    public static RhFiltro ToFiltro(CargoFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);
    public static RhFiltro ToFiltro(LotacaoFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);
    public static RhFiltro ToFiltro(VinculoFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);
    public static RhFiltro ToFiltro(FolhaFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);
    public static RhFiltro ToFiltro(PontoFiltro filtro) => new(filtro.Page, filtro.PageSize, filtro.Termo, filtro.Ativo);

    public static RhRegistroCreateRequest ToCreate(ServidorCreateRequest request) => new(Dados(
        ("matricula", request.Matricula), ("nome", request.Nome), ("cpf", request.Cpf), ("dataNascimento", request.DataNascimento), ("email", request.Email),
        ("emailInstitucional", request.EmailInstitucional), ("telefone", request.Telefone), ("pessoaId", request.PessoaId), ("banco", request.Banco), ("agencia", request.Agencia), ("conta", request.Conta), ("classificacaoLgpd", "dados_pessoais_sensiveis")));
    public static RhRegistroUpdateRequest ToUpdate(ServidorUpdateRequest request) => new(ToCreate(new ServidorCreateRequest(request.Matricula, request.Nome, request.Cpf, request.DataNascimento, request.Email, request.EmailInstitucional, request.Telefone, request.PessoaId, request.Banco, request.Agencia, request.Conta)).Dados, request.Ativo);

    public static RhRegistroCreateRequest ToCreate(CargoCreateRequest request) => new(Dados(("codigo", request.Codigo), ("nome", request.Nome), ("cbo", request.Cbo), ("vencimentoBase", request.VencimentoBase)));
    public static RhRegistroUpdateRequest ToUpdate(CargoUpdateRequest request) => new(ToCreate(new CargoCreateRequest(request.Codigo, request.Nome, request.Cbo, request.VencimentoBase)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(LotacaoCreateRequest request) => new(Dados(("codigo", request.Codigo), ("nome", request.Nome), ("lotacaoPaiId", request.LotacaoPaiId)));
    public static RhRegistroUpdateRequest ToUpdate(LotacaoUpdateRequest request) => new(ToCreate(new LotacaoCreateRequest(request.Codigo, request.Nome, request.LotacaoPaiId)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(VinculoCreateRequest request) => new(Dados(("servidorId", request.ServidorId), ("cargoId", request.CargoId), ("lotacaoId", request.LotacaoId), ("tipo", request.Tipo), ("dataAdmissao", request.DataAdmissao), ("dataDesligamento", request.DataDesligamento)));
    public static RhRegistroUpdateRequest ToUpdate(VinculoUpdateRequest request) => new(ToCreate(new VinculoCreateRequest(request.ServidorId, request.CargoId, request.LotacaoId, request.Tipo, request.DataAdmissao, request.DataDesligamento)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(FolhaCreateRequest request) => new(Dados(("ano", request.Ano), ("mes", request.Mes), ("tipo", request.Tipo), ("status", request.Status)));
    public static RhRegistroUpdateRequest ToUpdate(FolhaUpdateRequest request) => new(ToCreate(new FolhaCreateRequest(request.Ano, request.Mes, request.Tipo, request.Status)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(FolhaEventoCreateRequest request) => new(Dados(("codigo", request.Codigo), ("descricao", request.Descricao), ("tipo", request.Tipo), ("incideInss", request.IncideInss), ("incideIrrf", request.IncideIrrf)));
    public static RhRegistroUpdateRequest ToUpdate(FolhaEventoUpdateRequest request) => new(ToCreate(new FolhaEventoCreateRequest(request.Codigo, request.Descricao, request.Tipo, request.IncideInss, request.IncideIrrf)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(FolhaLancamentoCreateRequest request) => new(Dados(("folhaId", request.FolhaId), ("servidorId", request.ServidorId), ("eventoId", request.EventoId), ("valor", request.Valor), ("historico", request.Historico)));
    public static RhRegistroCreateRequest ToCreate(PontoCreateRequest request) => new(Dados(("servidorId", request.ServidorId), ("dataHora", request.DataHora), ("tipo", request.Tipo), ("origem", request.Origem)));
    public static RhRegistroCreateRequest ToCreate(FeriasCreateRequest request) => new(Dados(("servidorId", request.ServidorId), ("inicio", request.Inicio), ("fim", request.Fim), ("status", request.Status)));
    public static RhRegistroUpdateRequest ToUpdate(FeriasUpdateRequest request) => new(ToCreate(new FeriasCreateRequest(request.ServidorId, request.Inicio, request.Fim, request.Status)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(AfastamentoCreateRequest request) => new(Dados(("servidorId", request.ServidorId), ("inicio", request.Inicio), ("fim", request.Fim), ("motivo", request.Motivo), ("status", request.Status), ("motivoSensivel", request.Motivo)));
    public static RhRegistroUpdateRequest ToUpdate(AfastamentoUpdateRequest request) => new(ToCreate(new AfastamentoCreateRequest(request.ServidorId, request.Inicio, request.Fim, request.Motivo, request.Status)).Dados, request.Ativo);
    public static RhRegistroCreateRequest ToCreate(SaudeOcupacionalCreateRequest request) => new(Dados(("servidorId", request.ServidorId), ("tipo", request.Tipo), ("dataAtendimento", request.DataAtendimento), ("status", request.Status), ("resultadoExame", request.ResultadoExame), ("observacaoSaude", request.Observacao)));
    public static RhRegistroCreateRequest ToCreate(EsocialEventoCreateRequest request) => new(Dados(("evento", request.Evento), ("servidorId", request.ServidorId), ("status", request.Status), ("protocolo", request.Protocolo)));

    public static ServidorResponse ToServidor(RhRegistroResponse source)
    {
        var dados = RhLgpdMaskingPolicy.Mask(source.Dados);
        return new ServidorResponse(source.Id, Text(dados, "matricula"), Text(dados, "nome"), Text(dados, "cpf"), Date(dados, "dataNascimento"), TextOrNull(dados, "email"), TextOrNull(dados, "emailInstitucional"), TextOrNull(dados, "telefone"), Long(dados, "pessoaId"), source.Ativo, source.CreatedAt, source.UpdatedAt, Text(dados, "classificacaoLgpd", "dados_pessoais_sensiveis"));
    }

    public static CargoResponse ToCargo(RhRegistroResponse source) => new(source.Id, Text(source.Dados, "codigo"), Text(source.Dados, "nome"), TextOrNull(source.Dados, "cbo"), Decimal(source.Dados, "vencimentoBase"), source.Ativo);
    public static LotacaoResponse ToLotacao(RhRegistroResponse source) => new(source.Id, Text(source.Dados, "codigo"), Text(source.Dados, "nome"), Long(source.Dados, "lotacaoPaiId"), source.Ativo);
    public static VinculoResponse ToVinculo(RhRegistroResponse source) => new(source.Id, Long(source.Dados, "servidorId") ?? 0, Long(source.Dados, "cargoId") ?? 0, Long(source.Dados, "lotacaoId") ?? 0, Text(source.Dados, "tipo"), Date(source.Dados, "dataAdmissao"), Date(source.Dados, "dataDesligamento"), source.Ativo);
    public static FolhaResponse ToFolha(RhRegistroResponse source) => new(source.Id, Int(source.Dados, "ano") ?? 0, Int(source.Dados, "mes") ?? 0, Text(source.Dados, "tipo"), Text(source.Dados, "status", "Aberta"), source.Ativo);
    public static FolhaEventoResponse ToFolhaEvento(RhRegistroResponse source) => new(source.Id, Text(source.Dados, "codigo"), Text(source.Dados, "descricao"), Text(source.Dados, "tipo"), Bool(source.Dados, "incideInss"), Bool(source.Dados, "incideIrrf"), source.Ativo);
    public static FolhaLancamentoResponse ToFolhaLancamento(RhRegistroResponse source) => new(source.Id, Long(source.Dados, "folhaId") ?? 0, Long(source.Dados, "servidorId") ?? 0, Long(source.Dados, "eventoId") ?? 0, Decimal(source.Dados, "valor") ?? 0m, TextOrNull(source.Dados, "historico"), source.Ativo);
    public static PontoResponse ToPonto(RhRegistroResponse source) => new(source.Id, Long(source.Dados, "servidorId") ?? 0, DateTime(source.Dados, "dataHora"), Text(source.Dados, "tipo"), TextOrNull(source.Dados, "origem"), source.Ativo);
    public static FeriasResponse ToFerias(RhRegistroResponse source) => new(source.Id, Long(source.Dados, "servidorId") ?? 0, Date(source.Dados, "inicio"), Date(source.Dados, "fim"), Text(source.Dados, "status", "Programada"), source.Ativo);
    public static AfastamentoResponse ToAfastamento(RhRegistroResponse source)
    {
        var dados = RhLgpdMaskingPolicy.Mask(source.Dados);
        return new AfastamentoResponse(source.Id, Long(dados, "servidorId") ?? 0, Date(dados, "inicio"), Date(dados, "fim"), Text(dados, "motivo", "***"), Text(dados, "status", "Solicitado"), source.Ativo);
    }
    public static SaudeOcupacionalResponse ToSaudeOcupacional(RhRegistroResponse source)
    {
        var dados = RhLgpdMaskingPolicy.Mask(source.Dados);
        return new SaudeOcupacionalResponse(source.Id, Long(dados, "servidorId") ?? 0, Text(dados, "tipo"), Date(dados, "dataAtendimento"), Text(dados, "status"), TextOrNull(dados, "resultadoExame"), TextOrNull(dados, "observacaoSaude"), source.Ativo);
    }
    public static EsocialEventoResponse ToEsocial(RhRegistroResponse source) => new(source.Id, Text(source.Dados, "evento"), Long(source.Dados, "servidorId") ?? 0, Text(source.Dados, "status", "Pendente"), TextOrNull(source.Dados, "protocolo"), source.Ativo);
    public static PortalServidorResponse ToPortal(RhPortalResumoResponse source) => new(source.ServidorId, source.Nome, source.Contracheques.Select(c => new ContrachequeResumoResponse(c.Id, Decimal(c.Dados, "valor") ?? 0m, TextOrNull(c.Dados, "competencia"), TextOrNull(c.Dados, "historico"))).ToArray(), source.Ferias.Select(ToFerias).ToArray(), source.Afastamentos.Select(ToAfastamento).ToArray());

    public static PagedResult<TTarget> MapPage<TTarget>(PagedResult<RhRegistroResponse> source, Func<RhRegistroResponse, TTarget> mapper) => new(source.Items.Select(mapper).ToArray(), source.Page, source.PageSize, source.TotalItems);

    private static Dictionary<string, object?> Dados(params (string Key, object? Value)[] pairs) => pairs.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => Normalize(p.Value), StringComparer.OrdinalIgnoreCase);
    private static object? Normalize(object? value) => value switch { DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture), _ => value };
    private static string Text(IReadOnlyDictionary<string, object?> dados, string key, string defaultValue = "") => TextOrNull(dados, key) ?? defaultValue;
    private static string? TextOrNull(IReadOnlyDictionary<string, object?> dados, string key) => !dados.TryGetValue(key, out var value) || value is null ? null : value is JsonElement element ? element.ToString() : Convert.ToString(value, CultureInfo.InvariantCulture);
    private static long? Long(IReadOnlyDictionary<string, object?> dados, string key) => long.TryParse(TextOrNull(dados, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static int? Int(IReadOnlyDictionary<string, object?> dados, string key) => int.TryParse(TextOrNull(dados, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static decimal? Decimal(IReadOnlyDictionary<string, object?> dados, string key) => decimal.TryParse(TextOrNull(dados, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static DateOnly? Date(IReadOnlyDictionary<string, object?> dados, string key) => DateOnly.TryParse(TextOrNull(dados, key), CultureInfo.InvariantCulture, DateTimeStyles.None, out var value) ? value : null;
    private static DateTimeOffset? DateTime(IReadOnlyDictionary<string, object?> dados, string key) => DateTimeOffset.TryParse(TextOrNull(dados, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
    private static bool Bool(IReadOnlyDictionary<string, object?> dados, string key) => bool.TryParse(TextOrNull(dados, key), out var value) && value;
}
