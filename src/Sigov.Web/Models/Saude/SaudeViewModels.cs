using System.ComponentModel.DataAnnotations;

namespace Sigov.Web.Models.Saude;

public sealed class UnidadeSaudeFormViewModel { [Required] public string Codigo { get; set; } = string.Empty; [Required] public string Nome { get; set; } = string.Empty; public string TipoUnidade { get; set; } = "BASICA"; public string Situacao { get; set; } = "ATIVA"; public string? Cnes { get; set; } public decimal? Latitude { get; set; } public decimal? Longitude { get; set; } }
public sealed class ProfissionalSaudeFormViewModel { public long PessoaId { get; set; } [Required] public string CodigoProfissional { get; set; } = string.Empty; public string TipoProfissional { get; set; } = "OUTRO"; public string Situacao { get; set; } = "ATIVO"; public long? UnidadeSaudeId { get; set; } }
public sealed class PacienteFormViewModel { public long PessoaId { get; set; } [Required] public string CodigoPaciente { get; set; } = string.Empty; public string? CartaoSus { get; set; } public string? ProntuarioNumero { get; set; } public string? GrupoSanguineo { get; set; } public string? Alergias { get; set; } public string Situacao { get; set; } = "ATIVO"; }
public sealed class AtendimentoSaudeFormViewModel { public long UnidadeSaudeId { get; set; } public long PacienteId { get; set; } public long? ProfissionalSaudeId { get; set; } public string TipoAtendimento { get; set; } = "CONSULTA"; public string? QueixaPrincipal { get; set; } }
public sealed class AgendaSaudeFormViewModel { public long UnidadeSaudeId { get; set; } public long? PacienteId { get; set; } public long? ProfissionalSaudeId { get; set; } public DateTimeOffset DataInicio { get; set; } = DateTimeOffset.Now; public DateTimeOffset DataFim { get; set; } = DateTimeOffset.Now.AddMinutes(30); public string TipoAgendamento { get; set; } = "CONSULTA"; public string Status { get; set; } = "AGENDADA"; }
public sealed class FarmaciaProdutoFormViewModel { [Required] public string Codigo { get; set; } = string.Empty; [Required] public string Nome { get; set; } = string.Empty; public string UnidadeMedida { get; set; } = "UN"; public string? PrincipioAtivo { get; set; } }
public sealed class DispensacaoFormViewModel { public long UnidadeSaudeId { get; set; } public long PacienteId { get; set; } public long FarmaciaProdutoId { get; set; } public decimal Quantidade { get; set; } = 1m; public string? Lote { get; set; } }
public sealed class VacinacaoFormViewModel { public long UnidadeSaudeId { get; set; } public long PacienteId { get; set; } public string Vacina { get; set; } = string.Empty; public string Dose { get; set; } = string.Empty; public DateOnly DataAplicacao { get; set; } = DateOnly.FromDateTime(DateTime.Today); }
public sealed class LaboratorioExameFormViewModel { public long PacienteId { get; set; } public long? UnidadeSaudeId { get; set; } public string TipoExame { get; set; } = string.Empty; public string? Observacao { get; set; } }
public sealed class RegulacaoFormViewModel { public long PacienteId { get; set; } public string TipoSolicitacao { get; set; } = "CONSULTA_ESPECIALIZADA"; public string Prioridade { get; set; } = "MEDIA"; public string Justificativa { get; set; } = string.Empty; public string? Especialidade { get; set; } }
public sealed class AcsMicroareaFormViewModel { public long UnidadeSaudeId { get; set; } public string Codigo { get; set; } = string.Empty; public string Nome { get; set; } = string.Empty; public long? ProfissionalAcsId { get; set; } }
public sealed class AcsDomicilioFormViewModel
{
    [Required, Display(Name = "Código do domicílio")] public string CodigoDomicilio { get; set; } = string.Empty;
    [Required, Display(Name = "Microárea")] public long? AcsMicroareaId { get; set; }
    [Required, Display(Name = "Endereço ou referência de localização"), StringLength(500)] public string EnderecoDescricao { get; set; } = string.Empty;
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
    [Range(0, 10000), Display(Name = "Precisão (m)")] public decimal? PrecisaoMetros { get; set; }
    public string Status { get; set; } = "ATIVO";
}
public sealed class AcsIndividuoFormViewModel
{
    [Required, Display(Name = "Pessoa/cidadão")] public long? PessoaId { get; set; }
    [Display(Name = "Cadastro de paciente")] public long? PacienteId { get; set; }
    [Required, Display(Name = "Domicílio/família")] public long? AcsCadastroDomiciliarId { get; set; }
    [Display(Name = "Condições acompanhadas")] public string[] CondicoesSaude { get; set; } = [];
    public string Status { get; set; } = "ATIVO";
}
public sealed class AcsVisitaFormViewModel
{
    [Required, Display(Name = "Agente comunitário")] public long? ProfissionalAcsId { get; set; }
    [Required, Display(Name = "Microárea")] public long? AcsMicroareaId { get; set; }
    [Display(Name = "Domicílio")] public long? AcsCadastroDomiciliarId { get; set; }
    [Display(Name = "Indivíduo")] public long? AcsCadastroIndividualId { get; set; }
    [Required, Display(Name = "Data e hora")] public DateTimeOffset DataVisita { get; set; } = DateTimeOffset.Now;
    [Required] public string Turno { get; set; } = "MANHA";
    [Required, Display(Name = "Tipo de visita")] public string TipoVisita { get; set; } = "ROTINA";
    [Required] public string Desfecho { get; set; } = "REALIZADA";
    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }
    [Range(0, 10000), Display(Name = "Precisão (m)")] public decimal? PrecisaoMetros { get; set; }
    [StringLength(2000)] public string? Observacao { get; set; }
}
public sealed class Saude360OperacaoViewModel
{
    public required string Titulo { get; init; }
    public required string Descricao { get; init; }
    public required string Api { get; init; }
    public string? ExportacaoApi { get; init; }
    public bool DadoSensivel { get; init; }
}
public sealed class SaudeDashboardViewModel { public string Titulo { get; set; } = "Dashboard Saúde/ACS"; }
