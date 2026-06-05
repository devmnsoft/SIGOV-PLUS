using Microsoft.Extensions.Logging;
using Sigov.Application.Common;
using Sigov.Application.Rh.Dto;
using Sigov.Domain.Common;

namespace Sigov.Application.Rh;

public sealed class RhTypedService : IRhTypedService
{
    private readonly IRhService _service;
    private readonly ILogger<RhTypedService> _logger;

    public RhTypedService(IRhService service, ILogger<RhTypedService> logger)
    {
        _service = service;
        _logger = logger;
    }

    public Task<Result<long>> CriarServidorAsync(ServidorCreateRequest request, CancellationToken ct) => CreateAsync("servidores", RhTypedMapper.ToCreate(request), ValidateServidor(request.Matricula, request.Nome, request.Cpf), ct);
    public Task<Result> AtualizarServidorAsync(long id, ServidorUpdateRequest request, CancellationToken ct) => UpdateAsync("servidores", id, RhTypedMapper.ToUpdate(request), ValidateServidor(request.Matricula, request.Nome, request.Cpf), ct);
    public async Task<Result<ServidorResponse>> ObterServidorAsync(long id, CancellationToken ct) => await GetAsync("servidores", id, RhTypedMapper.ToServidor, ct).ConfigureAwait(false);
    public async Task<Result<PagedResult<ServidorResponse>>> ListarServidoresAsync(ServidorFiltro filtro, CancellationToken ct) => await ListAsync("servidores", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToServidor, ct).ConfigureAwait(false);
    public Task<Result> ExcluirServidorAsync(long id, CancellationToken ct) => _service.ExcluirAsync("servidores", id, ct);

    public Task<Result<long>> CriarCargoAsync(CargoCreateRequest request, CancellationToken ct) => CreateAsync("cargos", RhTypedMapper.ToCreate(request), ValidateRequired(("codigo", request.Codigo), ("nome", request.Nome)), ct);
    public async Task<Result<PagedResult<CargoResponse>>> ListarCargosAsync(CargoFiltro filtro, CancellationToken ct) => await ListAsync("cargos", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToCargo, ct).ConfigureAwait(false);
    public Task<Result<long>> CriarLotacaoAsync(LotacaoCreateRequest request, CancellationToken ct) => CreateAsync("lotacoes", RhTypedMapper.ToCreate(request), ValidateRequired(("codigo", request.Codigo), ("nome", request.Nome)), ct);
    public async Task<Result<PagedResult<LotacaoResponse>>> ListarLotacoesAsync(LotacaoFiltro filtro, CancellationToken ct) => await ListAsync("lotacoes", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToLotacao, ct).ConfigureAwait(false);
    public Task<Result<long>> CriarVinculoAsync(VinculoCreateRequest request, CancellationToken ct) => CreateAsync("vinculos", RhTypedMapper.ToCreate(request), ValidateIds(("servidorId", request.ServidorId), ("cargoId", request.CargoId), ("lotacaoId", request.LotacaoId)), ct);
    public async Task<Result<PagedResult<VinculoResponse>>> ListarVinculosAsync(VinculoFiltro filtro, CancellationToken ct) => await ListAsync("vinculos", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToVinculo, ct).ConfigureAwait(false);
    public Task<Result<long>> CriarFolhaAsync(FolhaCreateRequest request, CancellationToken ct) => CreateAsync("folhas", RhTypedMapper.ToCreate(request), ValidateFolha(request.Ano, request.Mes, request.Tipo), ct);
    public async Task<Result<PagedResult<FolhaResponse>>> ListarFolhasAsync(FolhaFiltro filtro, CancellationToken ct) => await ListAsync("folhas", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToFolha, ct).ConfigureAwait(false);

    public async Task<Result> FecharFolhaAsync(long id, CancellationToken ct)
    {
        var folha = await _service.ObterAsync("folhas", id, ct).ConfigureAwait(false);
        if (folha.IsFailure || folha.Value is null) return Result.Failure(folha.Error ?? "Folha não encontrada.");
        var dados = new Dictionary<string, object?>(folha.Value.Dados, StringComparer.OrdinalIgnoreCase) { ["status"] = "Fechada" };
        return await _service.AtualizarAsync("folhas", id, new RhRegistroUpdateRequest(dados, folha.Value.Ativo), ct).ConfigureAwait(false);
    }

    public Task<Result<long>> CriarEventoFolhaAsync(FolhaEventoCreateRequest request, CancellationToken ct) => CreateAsync("folha-eventos", RhTypedMapper.ToCreate(request), ValidateRequired(("codigo", request.Codigo), ("descricao", request.Descricao), ("tipo", request.Tipo)), ct);
    public Task<Result<long>> CriarLancamentoFolhaAsync(FolhaLancamentoCreateRequest request, CancellationToken ct) => CreateAsync("folha-lancamentos", RhTypedMapper.ToCreate(request), request.Valor < 0 ? Result.Failure("Valor do lançamento não pode ser negativo.") : ValidateIds(("folhaId", request.FolhaId), ("servidorId", request.ServidorId), ("eventoId", request.EventoId)), ct);
    public Task<Result<long>> RegistrarPontoAsync(PontoCreateRequest request, CancellationToken ct) => CreateAsync("pontos", RhTypedMapper.ToCreate(request), ValidateIds(("servidorId", request.ServidorId)), ct);
    public async Task<Result<PagedResult<PontoResponse>>> ListarPontosAsync(PontoFiltro filtro, CancellationToken ct) => await ListAsync("pontos", RhTypedMapper.ToFiltro(filtro), RhTypedMapper.ToPonto, ct).ConfigureAwait(false);
    public Task<Result<long>> ProgramarFeriasAsync(FeriasCreateRequest request, CancellationToken ct) => CreateAsync("ferias", RhTypedMapper.ToCreate(request), request.Fim < request.Inicio ? Result.Failure("Fim das férias deve ser maior ou igual ao início.") : ValidateIds(("servidorId", request.ServidorId)), ct);
    public Task<Result<long>> RegistrarAfastamentoAsync(AfastamentoCreateRequest request, CancellationToken ct) => CreateAsync("afastamentos", RhTypedMapper.ToCreate(request), request.Fim.HasValue && request.Fim.Value < request.Inicio ? Result.Failure("Fim do afastamento deve ser maior ou igual ao início.") : ValidateIds(("servidorId", request.ServidorId)), ct);
    public Task<Result<long>> RegistrarSaudeOcupacionalAsync(SaudeOcupacionalCreateRequest request, CancellationToken ct) => CreateAsync("saude-ocupacional", RhTypedMapper.ToCreate(request), ValidateIds(("servidorId", request.ServidorId)), ct);
    public Task<Result<long>> CriarEventoEsocialAsync(EsocialEventoCreateRequest request, CancellationToken ct) => CreateAsync("esocial", RhTypedMapper.ToCreate(request), ValidateRequired(("evento", request.Evento)), ct);

    public async Task<Result<PortalServidorResponse>> ObterPortalServidorAsync(long servidorId, CancellationToken ct)
    {
        var result = await _service.PortalServidorAsync(servidorId, ct).ConfigureAwait(false);
        return result.IsFailure || result.Value is null ? Result<PortalServidorResponse>.Failure(result.Error ?? "Portal do servidor não encontrado.") : Result<PortalServidorResponse>.Success(RhTypedMapper.ToPortal(result.Value));
    }

    private async Task<Result<long>> CreateAsync(string recurso, RhRegistroCreateRequest request, Result validation, CancellationToken ct)
    {
        if (validation.IsFailure) return Result<long>.Failure(validation.Error ?? "Dados inválidos.");
        _logger.LogInformation("Criando recurso RH tipado {Recurso}.", recurso);
        return await _service.CriarAsync(recurso, request, ct).ConfigureAwait(false);
    }

    private async Task<Result> UpdateAsync(string recurso, long id, RhRegistroUpdateRequest request, Result validation, CancellationToken ct)
    {
        if (validation.IsFailure) return Result.Failure(validation.Error ?? "Dados inválidos.");
        _logger.LogInformation("Atualizando recurso RH tipado {Recurso} #{Id}.", recurso, id);
        return await _service.AtualizarAsync(recurso, id, request, ct).ConfigureAwait(false);
    }

    private async Task<Result<T>> GetAsync<T>(string recurso, long id, Func<RhRegistroResponse, T> mapper, CancellationToken ct)
    {
        var result = await _service.ObterAsync(recurso, id, ct).ConfigureAwait(false);
        return result.IsFailure || result.Value is null ? Result<T>.Failure(result.Error ?? "Registro não encontrado.") : Result<T>.Success(mapper(result.Value));
    }

    private async Task<Result<PagedResult<T>>> ListAsync<T>(string recurso, RhFiltro filtro, Func<RhRegistroResponse, T> mapper, CancellationToken ct)
    {
        var result = await _service.ListarAsync(recurso, filtro, ct).ConfigureAwait(false);
        return result.IsFailure || result.Value is null ? Result<PagedResult<T>>.Failure(result.Error ?? "Falha ao listar RH.") : Result<PagedResult<T>>.Success(RhTypedMapper.MapPage(result.Value, mapper));
    }

    private static Result ValidateServidor(string matricula, string nome, string cpf)
    {
        var required = ValidateRequired(("matricula", matricula), ("nome", nome), ("cpf", cpf));
        if (required.IsFailure) return required;
        var digits = new string(cpf.Where(char.IsDigit).ToArray());
        return digits.Length == 11 ? Result.Success() : Result.Failure("CPF deve conter 11 dígitos.");
    }

    private static Result ValidateFolha(int ano, int mes, string tipo)
    {
        if (ano < 1900) return Result.Failure("Ano da folha inválido.");
        if (mes is < 1 or > 13) return Result.Failure("Mês da folha deve estar entre 1 e 13.");
        return ValidateRequired(("tipo", tipo));
    }

    private static Result ValidateRequired(params (string Name, string? Value)[] fields)
    {
        foreach (var (name, value) in fields)
        {
            if (string.IsNullOrWhiteSpace(value)) return Result.Failure($"Campo obrigatório: {name}.");
        }

        return Result.Success();
    }

    private static Result ValidateIds(params (string Name, long Value)[] fields)
    {
        foreach (var (name, value) in fields)
        {
            if (value <= 0) return Result.Failure($"Identificador obrigatório: {name}.");
        }

        return Result.Success();
    }
}
