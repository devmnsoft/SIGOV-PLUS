using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Programas;

public sealed record AgroProgramaRuralFiltro(int Page = 1, int PageSize = 20, string? Busca = null, string? TipoPrograma = null, bool? Ativo = null);
public sealed record AgroProgramaRuralCreateRequest(string Codigo, string Nome, string TipoPrograma, string? Descricao, string? CriteriosJson, DateOnly? VigenciaInicio, DateOnly? VigenciaFim, bool Ativo = true);
public sealed record AgroProgramaRuralUpdateRequest(string Codigo, string Nome, string TipoPrograma, string? Descricao, string? CriteriosJson, DateOnly? VigenciaInicio, DateOnly? VigenciaFim, bool Ativo = true);
public sealed record AgroProgramaRuralResponse(long Id, long TenantId, long EntidadeId, string Codigo, string Nome, string TipoPrograma, string? Descricao, DateOnly? VigenciaInicio, DateOnly? VigenciaFim, bool Ativo);
public sealed record AgroBeneficioRuralCreateRequest(long? ProgramaId, string Codigo, string Nome, string TipoBeneficio, string? UnidadeMedida, decimal? ValorReferencia, decimal? QuantidadeLimite, string? CriteriosJson, bool Ativo = true);
public sealed record AgroBeneficioRuralUpdateRequest(long? ProgramaId, string Codigo, string Nome, string TipoBeneficio, string? UnidadeMedida, decimal? ValorReferencia, decimal? QuantidadeLimite, string? CriteriosJson, bool Ativo = true);
public sealed record AgroBeneficioRuralResponse(long Id, long TenantId, long EntidadeId, long? ProgramaId, string Codigo, string Nome, string TipoBeneficio, string? UnidadeMedida, decimal? ValorReferencia, decimal? QuantidadeLimite, bool Ativo);
public sealed record AgroBeneficioConcessaoFiltro(int Page = 1, int PageSize = 20, long? ProdutorId = null, string? Status = null);
public sealed record AgroBeneficioConcessaoCreateRequest(long BeneficioId, long ProdutorId, long? PropriedadeId, string? Numero, decimal? Quantidade, decimal? Valor, string? Observacao);
public sealed record AgroBeneficioConcessaoResponse(long Id, long TenantId, long EntidadeId, long? ExercicioId, long BeneficioId, long ProdutorId, long? PropriedadeId, string Numero, DateOnly DataSolicitacao, DateOnly? DataConcessao, decimal? Quantidade, decimal? Valor, string Status, string ProdutorNomeMascarado, string? Observacao);
public sealed record AutorizarBeneficioRuralRequest(string? Observacao);
public sealed record EntregarBeneficioRuralRequest(string? Observacao);
public sealed record IndeferirBeneficioRuralRequest(string? Motivo);
public sealed record CancelarBeneficioRuralRequest(string? Motivo);
public sealed record AgroInsumoCreateRequest(string Codigo, string Nome, string TipoInsumo, string UnidadeMedida, bool ControlaEstoque, long? ProdutoId, bool Ativo = true);
public sealed record AgroInsumoResponse(long Id, long TenantId, long EntidadeId, string Codigo, string Nome, string TipoInsumo, string UnidadeMedida, bool ControlaEstoque, long? ProdutoId, bool Ativo);
public sealed record AgroDistribuicaoInsumoCreateRequest(long InsumoId, long ProdutorId, long? PropriedadeId, long? ProgramaId, long? BeneficioConcessaoId, string? Numero, decimal Quantidade, decimal? ValorEstimado, string? Observacao);
public sealed record AgroDistribuicaoInsumoResponse(long Id, long TenantId, long EntidadeId, long? ExercicioId, long InsumoId, long ProdutorId, long? PropriedadeId, string Numero, DateOnly DataDistribuicao, decimal Quantidade, decimal? ValorEstimado, string Status, string ProdutorNomeMascarado);

public interface IAgroProgramasRepository
{
    Task<PagedResult<AgroProgramaRuralResponse>> ListarProgramasAsync(long tenantId, long entidadeId, AgroProgramaRuralFiltro filtro, CancellationToken ct);
    Task<AgroProgramaRuralResponse?> ObterProgramaAsync(long tenantId, long entidadeId, long id, CancellationToken ct);
    Task<long> CriarProgramaAsync(long tenantId, long entidadeId, long? usuarioId, AgroProgramaRuralCreateRequest request, CancellationToken ct);
    Task AtualizarProgramaAsync(long tenantId, long entidadeId, long id, long? usuarioId, AgroProgramaRuralUpdateRequest request, CancellationToken ct);
    Task ExcluirProgramaAsync(long tenantId, long entidadeId, long id, long? usuarioId, CancellationToken ct);
    Task<PagedResult<AgroBeneficioRuralResponse>> ListarBeneficiosAsync(long tenantId, long entidadeId, AgroProgramaRuralFiltro filtro, CancellationToken ct);
    Task<AgroBeneficioRuralResponse?> ObterBeneficioAsync(long tenantId, long entidadeId, long id, CancellationToken ct);
    Task<long> CriarBeneficioAsync(long tenantId, long entidadeId, long? usuarioId, AgroBeneficioRuralCreateRequest request, CancellationToken ct);
    Task AtualizarBeneficioAsync(long tenantId, long entidadeId, long id, long? usuarioId, AgroBeneficioRuralUpdateRequest request, CancellationToken ct);
    Task ExcluirBeneficioAsync(long tenantId, long entidadeId, long id, long? usuarioId, CancellationToken ct);
    Task<PagedResult<AgroBeneficioConcessaoResponse>> ListarConcessoesAsync(long tenantId, long entidadeId, AgroBeneficioConcessaoFiltro filtro, CancellationToken ct);
    Task<AgroBeneficioConcessaoResponse?> ObterConcessaoAsync(long tenantId, long entidadeId, long id, CancellationToken ct);
    Task<long> CriarConcessaoAsync(long tenantId, long entidadeId, long? exercicioId, long? usuarioId, AgroBeneficioConcessaoCreateRequest request, string numero, CancellationToken ct);
    Task AtualizarStatusConcessaoAsync(long tenantId, long entidadeId, long id, long? usuarioId, string status, string? observacao, CancellationToken ct);
    Task<PagedResult<AgroInsumoResponse>> ListarInsumosAsync(long tenantId, long entidadeId, AgroProgramaRuralFiltro filtro, CancellationToken ct);
    Task<long> CriarInsumoAsync(long tenantId, long entidadeId, long? usuarioId, AgroInsumoCreateRequest request, CancellationToken ct);
    Task<PagedResult<AgroDistribuicaoInsumoResponse>> ListarDistribuicoesAsync(long tenantId, long entidadeId, AgroBeneficioConcessaoFiltro filtro, CancellationToken ct);
    Task<long> CriarDistribuicaoAsync(long tenantId, long entidadeId, long? exercicioId, long? usuarioId, AgroDistribuicaoInsumoCreateRequest request, string numero, CancellationToken ct);
}
public interface IAgroProgramaRuralService { Task<Result<PagedResult<AgroProgramaRuralResponse>>> ListarAsync(AgroProgramaRuralFiltro filtro, CancellationToken ct); Task<Result<AgroProgramaRuralResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(AgroProgramaRuralCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, AgroProgramaRuralUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); }
public interface IAgroBeneficioRuralService { Task<Result<PagedResult<AgroBeneficioRuralResponse>>> ListarAsync(AgroProgramaRuralFiltro filtro, CancellationToken ct); Task<Result<AgroBeneficioRuralResponse>> ObterAsync(long id, CancellationToken ct); Task<Result<long>> CriarAsync(AgroBeneficioRuralCreateRequest request, CancellationToken ct); Task<Result> AtualizarAsync(long id, AgroBeneficioRuralUpdateRequest request, CancellationToken ct); Task<Result> ExcluirAsync(long id, CancellationToken ct); Task<Result<PagedResult<AgroBeneficioConcessaoResponse>>> ListarConcessoesAsync(AgroBeneficioConcessaoFiltro filtro, CancellationToken ct); Task<Result<AgroBeneficioConcessaoResponse>> ObterConcessaoAsync(long id, CancellationToken ct); Task<Result<long>> SolicitarConcessaoAsync(AgroBeneficioConcessaoCreateRequest request, CancellationToken ct); Task<Result> AutorizarAsync(long id, AutorizarBeneficioRuralRequest request, CancellationToken ct); Task<Result> EntregarAsync(long id, EntregarBeneficioRuralRequest request, CancellationToken ct); Task<Result> IndeferirAsync(long id, IndeferirBeneficioRuralRequest request, CancellationToken ct); Task<Result> CancelarAsync(long id, CancelarBeneficioRuralRequest request, CancellationToken ct); }
public interface IAgroInsumoService { Task<Result<PagedResult<AgroInsumoResponse>>> ListarAsync(AgroProgramaRuralFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(AgroInsumoCreateRequest request, CancellationToken ct); }
public interface IAgroDistribuicaoInsumoService { Task<Result<PagedResult<AgroDistribuicaoInsumoResponse>>> ListarAsync(AgroBeneficioConcessaoFiltro filtro, CancellationToken ct); Task<Result<long>> CriarAsync(AgroDistribuicaoInsumoCreateRequest request, CancellationToken ct); }
