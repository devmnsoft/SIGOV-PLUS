using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Workflows;

public sealed record WorkflowSummary(long Id, string Nome, string Modulo, string Status, int Versao, DateTimeOffset AtualizadoEm);
public sealed record WorkflowStep(long Id, string Nome, string? Descricao, string Tipo, int Ordem, int? PrazoHoras, bool Inicial, bool Final, bool ExigeJustificativa, bool ExigeAnexo, bool ExigeAprovacao, bool PermiteRetorno);
public sealed record WorkflowTransition(long Id, long DeEtapaId, long ParaEtapaId, string Acao, string? Condicao, string? PermissaoNecessaria, string? MensagemUsuario);
public sealed record WorkflowDesignerViewModel(WorkflowSummary Workflow, IReadOnlyList<WorkflowStep> Etapas, IReadOnlyList<WorkflowTransition> Transicoes, IReadOnlyList<string> Erros);

public sealed class CreateWorkflowInput
{
    [Required, StringLength(120, MinimumLength = 3)] public string Nome { get; set; } = string.Empty;
    [Required, RegularExpression("^(PROTOCOLO|GED|TAREFA|SOLICITACAO_EXTERNA|COMPRA_CONTRATO)$")] public string Modulo { get; set; } = "PROTOCOLO";
    [StringLength(500)] public string? Descricao { get; set; }
}

public sealed class SaveWorkflowDesignInput
{
    [Required] public List<WorkflowStepInput> Etapas { get; set; } = [];
    public List<WorkflowTransitionInput> Transicoes { get; set; } = [];
}
public sealed class WorkflowStepInput
{
    public long? Id { get; set; }
    [Required, StringLength(100)] public string Nome { get; set; } = string.Empty;
    [StringLength(400)] public string? Descricao { get; set; }
    [Required] public string Tipo { get; set; } = "ANALISE";
    public int Ordem { get; set; }
    [Range(1, 8760)] public int? PrazoHoras { get; set; }
    public bool Inicial { get; set; }
    public bool Final { get; set; }
    public bool ExigeJustificativa { get; set; }
    public bool ExigeAnexo { get; set; }
    public bool ExigeAprovacao { get; set; }
    public bool PermiteRetorno { get; set; }
}
public sealed class WorkflowTransitionInput
{
    public long DeEtapaId { get; set; }
    public long ParaEtapaId { get; set; }
    [Required, StringLength(80)] public string Acao { get; set; } = string.Empty;
    [StringLength(300)] public string? Condicao { get; set; }
    [StringLength(100)] public string? PermissaoNecessaria { get; set; }
    [StringLength(240)] public string? MensagemUsuario { get; set; }
}
