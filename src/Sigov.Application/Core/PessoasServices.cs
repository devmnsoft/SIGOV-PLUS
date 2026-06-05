using Microsoft.Extensions.Logging;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;
using Sigov.Domain.Common;
using Sigov.Domain.Core;

namespace Sigov.Application.Core;

public sealed class PessoaCadastroService : IPessoaCadastroService
{
    private readonly IPessoaCadastroRepository _repository;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IAuditService _audit;
    private readonly ILogger<PessoaCadastroService> _logger;

    public PessoaCadastroService(IPessoaCadastroRepository repository, ICurrentTenant tenant, ICurrentUser user, IAuditService audit, ILogger<PessoaCadastroService> logger)
    {
        _repository = repository;
        _tenant = tenant;
        _user = user;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Result<PagedResult<PessoaResumoResponse>>> ListarAsync(PessoaFiltro filtro, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure<PagedResult<PessoaResumoResponse>>();
        try
        {
            var result = await _repository.ListarAsync(_tenant.TenantId!.Value, filtro, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "PESSOA_CONSULTA", "sigov.pessoa", "LIST", null, new { filtro.Page, filtro.PageSize, filtro.Termo, filtro.TipoPessoa }, cancellationToken).ConfigureAwait(false);
            return Result<PagedResult<PessoaResumoResponse>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar pessoas. TenantId={TenantId}", _tenant.TenantId);
            return Result<PagedResult<PessoaResumoResponse>>.Failure("Erro ao listar pessoas.");
        }
    }

    public async Task<Result<PessoaDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure<PessoaDetalheResponse>();
        try
        {
            var result = await _repository.ObterAsync(_tenant.TenantId!.Value, id, cancellationToken).ConfigureAwait(false);
            if (result is null) return Result<PessoaDetalheResponse>.Failure("Pessoa não encontrada.");
            await _audit.RegistrarAsync("core", "ACESSO_DADO_PESSOAL", "sigov.pessoa", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, new { result.Nome, result.Documento }, cancellationToken).ConfigureAwait(false);
            return Result<PessoaDetalheResponse>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter pessoa {PessoaId}. TenantId={TenantId}", id, _tenant.TenantId);
            return Result<PessoaDetalheResponse>.Failure("Erro ao obter pessoa.");
        }
    }

    public async Task<Result<long>> CriarAsync(PessoaCreateRequest request, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure<long>();
        var validation = ValidarPessoa(request.TipoPessoa, request.Nome, request.Documento);
        if (validation.IsFailure) return Result<long>.Failure(validation.Error ?? "Dados inválidos.");
        try
        {
            var id = await _repository.CriarAsync(_tenant.TenantId!.Value, _tenant.EntidadeId, _tenant.ExercicioId, request, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "PESSOA_INCLUSAO", "sigov.pessoa", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request, cancellationToken).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar pessoa. TenantId={TenantId}", _tenant.TenantId);
            return Result<long>.Failure("Erro ao criar pessoa. Verifique duplicidade de documento e dados obrigatórios.");
        }
    }

    public async Task<Result> AtualizarAsync(long id, PessoaUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure();
        var validation = ValidarPessoa(request.TipoPessoa, request.Nome, request.Documento);
        if (validation.IsFailure) return Result.Failure(validation.Error ?? "Dados inválidos.");
        try
        {
            var anterior = await _repository.ObterAsync(_tenant.TenantId!.Value, id, cancellationToken).ConfigureAwait(false);
            if (anterior is null) return Result.Failure("Pessoa não encontrada.");
            await _repository.AtualizarAsync(_tenant.TenantId!.Value, id, request, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "PESSOA_ALTERACAO", "sigov.pessoa", id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, request, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar pessoa {PessoaId}. TenantId={TenantId}", id, _tenant.TenantId);
            return Result.Failure("Erro ao atualizar pessoa.");
        }
    }

    public async Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure();
        try
        {
            var anterior = await _repository.ObterAsync(_tenant.TenantId!.Value, id, cancellationToken).ConfigureAwait(false);
            if (anterior is null) return Result.Failure("Pessoa não encontrada.");
            await _repository.ExcluirAsync(_tenant.TenantId!.Value, id, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "PESSOA_EXCLUSAO", "sigov.pessoa", id.ToString(System.Globalization.CultureInfo.InvariantCulture), anterior, null, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir pessoa {PessoaId}. TenantId={TenantId}", id, _tenant.TenantId);
            return Result.Failure("Erro ao excluir pessoa.");
        }
    }

    public async Task<Result<long>> AdicionarEnderecoAsync(long pessoaId, EnderecoCreateRequest request, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure<long>();
        var validation = ValidarEndereco(request.Logradouro, request.Municipio, request.Uf);
        if (validation.IsFailure) return Result<long>.Failure(validation.Error ?? "Endereço inválido.");
        try
        {
            var id = await _repository.AdicionarEnderecoAsync(_tenant.TenantId!.Value, pessoaId, request, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "ENDERECO_INCLUSAO", "sigov.endereco", id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request, cancellationToken).ConfigureAwait(false);
            return Result<long>.Success(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar endereço para pessoa {PessoaId}. TenantId={TenantId}", pessoaId, _tenant.TenantId);
            return Result<long>.Failure("Erro ao adicionar endereço.");
        }
    }

    public async Task<Result> AtualizarEnderecoAsync(long pessoaId, long enderecoId, EnderecoUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure();
        var validation = ValidarEndereco(request.Logradouro, request.Municipio, request.Uf);
        if (validation.IsFailure) return Result.Failure(validation.Error ?? "Endereço inválido.");
        try
        {
            await _repository.AtualizarEnderecoAsync(_tenant.TenantId!.Value, pessoaId, enderecoId, request, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "ENDERECO_ALTERACAO", "sigov.endereco", enderecoId.ToString(System.Globalization.CultureInfo.InvariantCulture), null, request, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar endereço {EnderecoId}. TenantId={TenantId}", enderecoId, _tenant.TenantId);
            return Result.Failure("Erro ao atualizar endereço.");
        }
    }

    public async Task<Result> ExcluirEnderecoAsync(long pessoaId, long enderecoId, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure();
        try
        {
            await _repository.ExcluirEnderecoAsync(_tenant.TenantId!.Value, pessoaId, enderecoId, _user.UsuarioId, cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "ENDERECO_EXCLUSAO", "sigov.endereco", enderecoId.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null, cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir endereço {EnderecoId}. TenantId={TenantId}", enderecoId, _tenant.TenantId);
            return Result.Failure("Erro ao excluir endereço.");
        }
    }

    public async Task<Result<byte[]>> ExportarAsync(string formato, CancellationToken cancellationToken)
    {
        if (!EscopoValido) return TenantFailure<byte[]>();
        if (!new[] { "csv", "json", "xml" }.Contains(formato, StringComparer.OrdinalIgnoreCase)) return Result<byte[]>.Failure("Formato de exportação inválido.");
        try
        {
            var result = await _repository.ExportarAsync(_tenant.TenantId!.Value, formato.ToLowerInvariant(), cancellationToken).ConfigureAwait(false);
            await _audit.RegistrarAsync("core", "PESSOA_EXPORTACAO", "sigov.pessoa", formato, null, new { formato }, cancellationToken).ConfigureAwait(false);
            return Result<byte[]>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao exportar pessoas. TenantId={TenantId} Formato={Formato}", _tenant.TenantId, formato);
            return Result<byte[]>.Failure("Erro ao exportar pessoas.");
        }
    }

    private bool EscopoValido => _tenant.TenantId.HasValue;
    private static Result TenantFailure() => Result.Failure("Tenant não resolvido. Informe X-Tenant-Slug ou domínio cadastrado.");
    private static Result<T> TenantFailure<T>() => Result<T>.Failure("Tenant não resolvido. Informe X-Tenant-Slug ou domínio cadastrado.");

    private static Result ValidarPessoa(string tipoPessoa, string nome, string? documento)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(nome)) return Result.Failure("Nome é obrigatório.");
            var tipo = tipoPessoa.Equals("J", StringComparison.OrdinalIgnoreCase) ? TipoPessoa.Juridica : TipoPessoa.Fisica;
            _ = new Pessoa(tipo, nome, documento);
            return Result.Success();
        }
        catch (ArgumentException ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    private static Result ValidarEndereco(string logradouro, string municipio, string uf)
    {
        if (string.IsNullOrWhiteSpace(logradouro)) return Result.Failure("Logradouro é obrigatório.");
        if (string.IsNullOrWhiteSpace(municipio)) return Result.Failure("Município é obrigatório.");
        if (string.IsNullOrWhiteSpace(uf) || uf.Trim().Length != 2) return Result.Failure("UF deve possuir 2 caracteres.");
        return Result.Success();
    }
}
