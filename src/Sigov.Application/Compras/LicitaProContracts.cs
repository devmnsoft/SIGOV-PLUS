using System.ComponentModel.DataAnnotations;

namespace Sigov.Application.Compras;

public static class LicitaProPermissoes
{
    public const string Dashboard = "compras.licitapro.dashboard.visualizar";
    public const string FonteVer = "compras.licitapro.fonte.visualizar";
    public const string FonteGerir = "compras.licitapro.fonte.gerenciar";
    public const string OportunidadeVer = "compras.licitapro.oportunidade.visualizar";
    public const string OportunidadeGerir = "compras.licitapro.oportunidade.gerenciar";
    public const string PortalVer = "compras.licitapro.fornecedor_portal.visualizar";
    public const string DocumentoVer = "compras.licitapro.documento.visualizar";
    public const string DocumentoGerir = "compras.licitapro.documento.gerenciar";
    public const string ChecklistVer = "compras.licitapro.checklist.visualizar";
    public const string AnaliseVer = "compras.licitapro.analise.visualizar";
    public const string AgendaVer = "compras.licitapro.agenda.visualizar";
    public const string AgendaGerir = "compras.licitapro.agenda.gerenciar";
    public const string Exportar = "compras.licitapro.relatorio.exportar";
    public const string AuditoriaVer = "compras.licitapro.auditoria.visualizar";
}

public sealed record LicitaProDashboard(long Abertas,long Vencendo,long Vencidas,long Vinculadas,long FornecedoresDocumentoVencido,long ChecklistsPendentes,long PropostasPreparacao,long ContratosConquistados,decimal AderenciaMedia);
public sealed record LicitaProFonte(long Id,string Nome,string Tipo,bool Configurada,bool Ativa,DateTimeOffset? UltimaSincronizacaoAt,string Estado);
public sealed record LicitaProOportunidade(long Id,string Numero,string Objeto,string Modalidade,string Fonte,DateOnly DataPublicacao,DateOnly? DataLimite,string Status,long? ProcessoId);
public sealed record LicitaProLinha(long Id,string Titulo,string Contexto,string Status,DateTimeOffset? Prazo=null);
public sealed record LicitaProOpcao(long Id,string Texto);
public sealed record LicitaProWorkspace(
    string Area,
    string Titulo,
    string Descricao,
    LicitaProFiltro Filtro,
    IReadOnlyList<LicitaProLinha> Linhas,
    IReadOnlyList<LicitaProOpcao>? Fornecedores = null,
    IReadOnlyList<LicitaProOpcao>? Processos = null,
    IReadOnlyList<LicitaProOpcao>? Oportunidades = null,
    IReadOnlyList<LicitaProOpcao>? Contratos = null,
    DocumentoFornecedorInput? Documento = null,
    AgendaPropostaInput? Agenda = null);
public sealed record LicitaProFiltro(string? Busca=null,string? Status=null,long? FonteId=null,DateOnly? De=null,DateOnly? Ate=null);

public sealed class OportunidadeInput : IValidatableObject
{
    [Required] public long? FonteId { get; set; }
    [Required, StringLength(180)] public string IdentificadorExterno { get; set; } = "";
    [Required, StringLength(120)] public string Numero { get; set; } = "";
    [Required, StringLength(4000)] public string Objeto { get; set; } = "";
    [Required, StringLength(100)] public string Modalidade { get; set; } = "";
    [Required] public DateOnly? DataPublicacao { get; set; }
    public DateOnly? DataLimite { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext _) { if (DataLimite < DataPublicacao) yield return new("A data limite não pode ser anterior à publicação.", [nameof(DataLimite)]); }
}
public sealed class VinculoOportunidadeInput { [Required] public long? ProcessoId { get; set; } }
public sealed class DocumentoFornecedorInput : IValidatableObject
{
    [Required] public long? FornecedorId { get; set; }
    [Required, StringLength(80)] public string Tipo { get; set; } = "";
    [Required, StringLength(180)] public string Titulo { get; set; } = "";
    public DateOnly? Validade { get; set; }
    [StringLength(1000)] public string? ReferenciaDocumental { get; set; }
    [Required] public string Status { get; set; } = "PENDENTE";

    public IEnumerable<ValidationResult> Validate(ValidationContext _)
    {
        if (Status == "APROVADO" && (Validade is null || string.IsNullOrWhiteSpace(ReferenciaDocumental)))
            yield return new("Documento aprovado exige validade e referência documental.", [nameof(Status), nameof(Validade), nameof(ReferenciaDocumental)]);
    }
}
public sealed class AgendaPropostaInput
{
    [Required] public long? OportunidadeId { get; set; }
    [Required] public long? ProcessoId { get; set; }
    [Required] public long? FornecedorId { get; set; }
    public long? ContratoId { get; set; }
    [Required, StringLength(180)] public string Titulo { get; set; } = "";
    [Required] public DateTimeOffset? PrazoAt { get; set; }
}
public interface ILicitaProService
{
    Task<LicitaProDashboard> DashboardAsync(long tenantId,long entidadeId,CancellationToken ct);
    Task<IReadOnlyList<LicitaProFonte>> FontesAsync(long tenantId,long entidadeId,CancellationToken ct);
    Task<IReadOnlyList<LicitaProOportunidade>> OportunidadesAsync(long tenantId,long entidadeId,LicitaProFiltro filtro,CancellationToken ct);
    Task<LicitaProOportunidade?> OportunidadeAsync(long tenantId,long entidadeId,long id,CancellationToken ct);
    Task<LicitaProWorkspace> WorkspaceAsync(long tenantId,long entidadeId,string area,LicitaProFiltro filtro,CancellationToken ct);
    Task<long> CriarOportunidadeAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,OportunidadeInput input,CancellationToken ct);
    Task VincularAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,long id,long processoId,CancellationToken ct);
    Task<long> CriarDocumentoAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,DocumentoFornecedorInput input,CancellationToken ct);
    Task<long> CriarAgendaAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,AgendaPropostaInput input,CancellationToken ct);
    Task<byte[]> ExportarAsync(long tenantId,long entidadeId,long usuarioId,string correlationId,string area,LicitaProFiltro filtro,CancellationToken ct);
}
