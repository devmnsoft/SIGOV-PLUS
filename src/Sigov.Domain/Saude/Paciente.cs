using Sigov.Domain.Common;

namespace Sigov.Domain.Saude;

public enum UnidadeSaudeTipo { Basica, Especializada, ProntoAtendimento, Caps, Outro }
public enum UnidadeSaudeSituacao { Ativa, Inativa }
public enum ProfissionalSaudeTipo { Medico, Enfermeiro, TecnicoEnfermagem, Dentista, Acs, Outro }
public enum ProfissionalSaudeSituacao { Ativo, Inativo }
public enum PacienteSituacao { Ativo, Inativo, Obito }
public enum AtendimentoSaudeTipo { Consulta, Enfermagem, Odontologia, Procedimento, Visita, Outro }
public enum AtendimentoSaudeStatus { Agendado, EmAtendimento, Atendido, Cancelado, Faltou }
public enum AgendaSaudeStatus { Agendada, Confirmada, Cancelada, Realizada, Faltou }
public enum LaboratorioExameStatus { Solicitado, Coletado, Concluido, Cancelado }
public enum RegulacaoPrioridade { Baixa, Media, Alta, Urgente }
public enum RegulacaoStatus { Solicitada, EmAnalise, Autorizada, Negada, Cancelada }
public enum AcsDispositivoStatus { Ativo, Bloqueado, Inativo }
public enum AcsCadastroStatus { Ativo, Inativo, Incompleto }
public enum AcsVisitaTipo { Rotina, BuscaAtiva, Acompanhamento, Outro }
public enum AcsVisitaDesfecho { Realizada, Ausente, Recusada, Reagendada }
public enum AcsSyncStatus { Recebido, Processado, ProcessadoComErros, Erro }

public sealed class UnidadeSaude : AggregateRoot
{
    public UnidadeSaude(string codigo, string nome)
    {
        Codigo = Required(codigo, nameof(codigo));
        Nome = Required(nome, nameof(nome));
    }
    public string Codigo { get; }
    public string Nome { get; }
    private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
}

public sealed class ProfissionalSaude : AggregateRoot
{
    public ProfissionalSaude(long pessoaId, string codigoProfissional)
    {
        PessoaId = pessoaId > 0 ? pessoaId : throw new ArgumentException("Profissional deve estar vinculado a uma pessoa.", nameof(pessoaId));
        CodigoProfissional = string.IsNullOrWhiteSpace(codigoProfissional) ? throw new ArgumentException("Código profissional é obrigatório.", nameof(codigoProfissional)) : codigoProfissional.Trim();
    }
    public long PessoaId { get; }
    public string CodigoProfissional { get; }
}

public sealed class Paciente : AggregateRoot
{
    public Paciente(long pessoaId, string codigoPaciente)
    {
        PessoaId = pessoaId > 0 ? pessoaId : throw new ArgumentException("Paciente deve estar vinculado a uma pessoa.", nameof(pessoaId));
        CodigoPaciente = string.IsNullOrWhiteSpace(codigoPaciente) ? throw new ArgumentException("Código do paciente é obrigatório.", nameof(codigoPaciente)) : codigoPaciente.Trim();
    }
    public long PessoaId { get; }
    public string CodigoPaciente { get; }
    public bool DadosSensiveis => true;
}

public sealed class Prontuario : AggregateRoot
{
    public Prontuario(long pacienteId, string numero)
    {
        PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Prontuário deve pertencer a um paciente.", nameof(pacienteId));
        Numero = string.IsNullOrWhiteSpace(numero) ? throw new ArgumentException("Número do prontuário é obrigatório.", nameof(numero)) : numero.Trim();
    }
    public long PacienteId { get; }
    public string Numero { get; }
}

public sealed class AtendimentoSaude : AggregateRoot
{
    public AtendimentoSaude(long unidadeSaudeId, long pacienteId, string numero)
    {
        UnidadeSaudeId = unidadeSaudeId > 0 ? unidadeSaudeId : throw new ArgumentException("Atendimento exige unidade.", nameof(unidadeSaudeId));
        PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Atendimento exige paciente.", nameof(pacienteId));
        Numero = string.IsNullOrWhiteSpace(numero) ? throw new ArgumentException("Número do atendimento é obrigatório.", nameof(numero)) : numero.Trim();
    }
    public long UnidadeSaudeId { get; }
    public long PacienteId { get; }
    public string Numero { get; }
    public AtendimentoSaudeStatus Status { get; private set; } = AtendimentoSaudeStatus.Agendado;
    public string? Conduta { get; private set; }
    public void Cancelar() => Status = AtendimentoSaudeStatus.Cancelado;
    public void RegistrarConduta(string conduta)
    {
        if (Status == AtendimentoSaudeStatus.Cancelado) throw new InvalidOperationException("Atendimento cancelado não pode receber conduta.");
        Conduta = string.IsNullOrWhiteSpace(conduta) ? throw new ArgumentException("Conduta é obrigatória.", nameof(conduta)) : conduta.Trim();
        Status = AtendimentoSaudeStatus.Atendido;
    }
}

public sealed class AgendaSaude : AggregateRoot
{
    public AgendaSaude(long unidadeSaudeId, DateTimeOffset inicio, DateTimeOffset fim)
    {
        UnidadeSaudeId = unidadeSaudeId > 0 ? unidadeSaudeId : throw new ArgumentException("Agenda exige unidade.", nameof(unidadeSaudeId));
        if (inicio >= fim) throw new ArgumentException("Data inicial deve ser menor que data final.", nameof(fim));
        DataInicio = inicio; DataFim = fim;
    }
    public long UnidadeSaudeId { get; }
    public DateTimeOffset DataInicio { get; }
    public DateTimeOffset DataFim { get; }
}

public sealed class FarmaciaProduto : AggregateRoot { public FarmaciaProduto(string codigo, string nome) { Codigo = Required(codigo, nameof(codigo)); Nome = Required(nome, nameof(nome)); } public string Codigo { get; } public string Nome { get; } private static string Required(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim(); }
public sealed class FarmaciaEstoque : AggregateRoot { public FarmaciaEstoque(decimal quantidade) { Quantidade = quantidade < 0m ? throw new ArgumentException("Estoque não pode ser negativo.", nameof(quantidade)) : quantidade; } public decimal Quantidade { get; private set; } public void Baixar(decimal quantidade) { if (quantidade <= 0m) throw new ArgumentException("Quantidade deve ser positiva.", nameof(quantidade)); if (Quantidade - quantidade < 0m) throw new InvalidOperationException("Dispensação não pode deixar estoque negativo."); Quantidade -= quantidade; } }
public sealed class FarmaciaDispensacao : AggregateRoot { public FarmaciaDispensacao(long pacienteId, long produtoId, decimal quantidade) { PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Paciente obrigatório.", nameof(pacienteId)); ProdutoId = produtoId > 0 ? produtoId : throw new ArgumentException("Produto obrigatório.", nameof(produtoId)); Quantidade = quantidade > 0m ? quantidade : throw new ArgumentException("Dispensação exige quantidade positiva.", nameof(quantidade)); } public long PacienteId { get; } public long ProdutoId { get; } public decimal Quantidade { get; } }
public sealed class Vacinacao : AggregateRoot { public Vacinacao(long pacienteId, string vacina, string dose, DateOnly dataAplicacao) { PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Vacinação exige paciente.", nameof(pacienteId)); Vacina = string.IsNullOrWhiteSpace(vacina) ? throw new ArgumentException("Vacina obrigatória.", nameof(vacina)) : vacina.Trim(); Dose = string.IsNullOrWhiteSpace(dose) ? throw new ArgumentException("Dose obrigatória.", nameof(dose)) : dose.Trim(); DataAplicacao = dataAplicacao; } public long PacienteId { get; } public string Vacina { get; } public string Dose { get; } public DateOnly DataAplicacao { get; } }
public sealed class LaboratorioExame : AggregateRoot { public LaboratorioExame(long pacienteId, string tipoExame) { PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Exame exige paciente.", nameof(pacienteId)); TipoExame = string.IsNullOrWhiteSpace(tipoExame) ? throw new ArgumentException("Tipo de exame obrigatório.", nameof(tipoExame)) : tipoExame.Trim(); } public long PacienteId { get; } public string TipoExame { get; } public LaboratorioExameStatus Status { get; private set; } = LaboratorioExameStatus.Solicitado; public void Concluir(string resultadoJson) { if (string.IsNullOrWhiteSpace(resultadoJson) || resultadoJson.Trim() == "{}") throw new InvalidOperationException("Exame concluído deve ter resultado."); Status = LaboratorioExameStatus.Concluido; } }
public sealed class RegulacaoSolicitacao : AggregateRoot { public RegulacaoSolicitacao(long pacienteId, string justificativa) { PacienteId = pacienteId > 0 ? pacienteId : throw new ArgumentException("Regulação exige paciente.", nameof(pacienteId)); Justificativa = string.IsNullOrWhiteSpace(justificativa) ? throw new ArgumentException("Regulação exige justificativa.", nameof(justificativa)) : justificativa.Trim(); } public long PacienteId { get; } public string Justificativa { get; } }
public sealed class AcsMicroarea : AggregateRoot { public AcsMicroarea(string codigo, string nome) { Codigo = string.IsNullOrWhiteSpace(codigo) ? throw new ArgumentException("Código obrigatório.", nameof(codigo)) : codigo.Trim(); Nome = string.IsNullOrWhiteSpace(nome) ? throw new ArgumentException("Nome obrigatório.", nameof(nome)) : nome.Trim(); } public string Codigo { get; } public string Nome { get; } }
public sealed class AcsDispositivo : AggregateRoot { public AcsDispositivo(long profissionalAcsId, string identificador) { ProfissionalAcsId = profissionalAcsId > 0 ? profissionalAcsId : throw new ArgumentException("ACS obrigatório.", nameof(profissionalAcsId)); Identificador = string.IsNullOrWhiteSpace(identificador) ? throw new ArgumentException("Identificador obrigatório.", nameof(identificador)) : identificador.Trim(); } public long ProfissionalAcsId { get; } public string Identificador { get; } }
public sealed class AcsCadastroDomiciliar : AggregateRoot { public AcsCadastroDomiciliar(string? enderecoJson, decimal? latitude, decimal? longitude) { if (string.IsNullOrWhiteSpace(enderecoJson) && (!latitude.HasValue || !longitude.HasValue)) throw new ArgumentException("Cadastro domiciliar ACS exige endereço ou geolocalização mínima."); if (latitude.HasValue || longitude.HasValue) GeoValidator.Validar(latitude, longitude); EnderecoJson = enderecoJson; Latitude = latitude; Longitude = longitude; } public string? EnderecoJson { get; } public decimal? Latitude { get; } public decimal? Longitude { get; } }
public sealed class AcsCadastroIndividual : AggregateRoot { public AcsCadastroIndividual(long pessoaId) { PessoaId = pessoaId > 0 ? pessoaId : throw new ArgumentException("Cadastro individual exige pessoa.", nameof(pessoaId)); } public long PessoaId { get; } public bool DadosSensiveis => true; }
public sealed class AcsVisita : AggregateRoot { public AcsVisita(long profissionalAcsId, long? domicilioId, long? individuoId, long? pacienteId, decimal? latitude = null, decimal? longitude = null) { ProfissionalAcsId = profissionalAcsId > 0 ? profissionalAcsId : throw new ArgumentException("Visita ACS exige ACS.", nameof(profissionalAcsId)); if (!domicilioId.HasValue && !individuoId.HasValue && !pacienteId.HasValue) throw new ArgumentException("Visita ACS exige domicílio, indivíduo ou paciente."); if (latitude.HasValue || longitude.HasValue) GeoValidator.Validar(latitude, longitude); } public long ProfissionalAcsId { get; } }
public sealed class AcsAtividadeColetiva : AggregateRoot { public AcsAtividadeColetiva(long profissionalAcsId, string tema) { ProfissionalAcsId = profissionalAcsId > 0 ? profissionalAcsId : throw new ArgumentException("Profissional ACS obrigatório.", nameof(profissionalAcsId)); Tema = string.IsNullOrWhiteSpace(tema) ? throw new ArgumentException("Tema obrigatório.", nameof(tema)) : tema.Trim(); } public long ProfissionalAcsId { get; } public string Tema { get; } }
public sealed class AcsSyncLote : AggregateRoot { private readonly HashSet<string> _offlineIds = new(StringComparer.OrdinalIgnoreCase); public AcsSyncLote(string loteId) { LoteId = string.IsNullOrWhiteSpace(loteId) ? throw new ArgumentException("loteId obrigatório.", nameof(loteId)) : loteId.Trim(); } public string LoteId { get; } public bool MesmoLote(string loteId) => string.Equals(LoteId, loteId, StringComparison.OrdinalIgnoreCase); public void AdicionarItem(AcsSyncItem item) { if (!_offlineIds.Add(item.OfflineId)) throw new InvalidOperationException("Sync item deve ser idempotente por offline_id dentro do lote."); } }
public sealed class AcsSyncItem : AggregateRoot { public AcsSyncItem(string offlineId, string tipoItem) { OfflineId = string.IsNullOrWhiteSpace(offlineId) ? throw new ArgumentException("offlineId obrigatório.", nameof(offlineId)) : offlineId.Trim(); TipoItem = string.IsNullOrWhiteSpace(tipoItem) ? throw new ArgumentException("tipoItem obrigatório.", nameof(tipoItem)) : tipoItem.Trim(); } public string OfflineId { get; } public string TipoItem { get; } }
public sealed class SaudeEvento : AggregateRoot { public SaudeEvento(string tipo, string payload) { Tipo = string.IsNullOrWhiteSpace(tipo) ? throw new ArgumentException("Tipo obrigatório.", nameof(tipo)) : tipo.Trim(); Payload = payload; } public string Tipo { get; } public string Payload { get; } }

internal static class GeoValidator
{
    public static void Validar(decimal? latitude, decimal? longitude)
    {
        if (!latitude.HasValue || !longitude.HasValue || latitude < -90m || latitude > 90m || longitude < -180m || longitude > 180m) throw new ArgumentException("Latitude/longitude inválidas.");
    }
}
