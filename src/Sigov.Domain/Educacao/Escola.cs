using Sigov.Domain.Common;

namespace Sigov.Domain.Educacao;

public enum EscolaTipo { Municipal, Conveniada, Rural, Urbana }
public enum EscolaSituacao { Ativa, Inativa, EmConstrucao, Extinta }
public enum AnoLetivoStatus { Planejado, Aberto, Encerrado, Cancelado }
public enum EtapaEnsino { EducacaoInfantil, EnsinoFundamental, EnsinoMedio, Eja, Especial }
public enum Turno { Matutino, Vespertino, Noturno, Integral }
public enum TurmaStatus { Planejada, Aberta, Fechada, Cancelada }
public enum AlunoSituacao { Ativo, Inativo, Transferido, Concluido, Evadido }
public enum MatriculaStatus { PreMatricula, Ativa, Transferida, Cancelada, Concluida, Evadida }
public enum ProfessorSituacao { Ativo, Inativo, Afastado }
public enum AvaliacaoStatus { Aberta, Fechada, Cancelada }
public enum PreMatriculaStatus { Recebida, EmAnalise, Deferida, Indeferida, ConvertidaMatricula, Cancelada }
public enum EducacensoStatus { Pendente, Validado, Erro, Enviado }

public abstract class EducacaoEntity : AggregateRoot
{
    protected EducacaoEntity(long tenantId, long entidadeId, long? exercicioId = null)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant obrigatório.", nameof(tenantId));
        if (entidadeId <= 0) throw new ArgumentException("Entidade obrigatória.", nameof(entidadeId));
        if (exercicioId.HasValue && exercicioId <= 0) throw new ArgumentException("Exercício inválido.", nameof(exercicioId));
        TenantId = tenantId;
        EntidadeId = entidadeId;
        ExercicioId = exercicioId;
    }

    public long TenantId { get; }
    public long EntidadeId { get; }
    public long? ExercicioId { get; }
    public bool Ativo { get; protected set; } = true;
    public bool IsDeleted { get; protected set; }
    protected static string Require(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
    protected static long RequireId(long value, string name) => value > 0 ? value : throw new ArgumentException("Identificador obrigatório.", name);
}

public sealed class Escola : EducacaoEntity
{
    public Escola(long tenantId, long entidadeId, string codigo, string nome, EscolaTipo tipo = EscolaTipo.Municipal, EscolaSituacao situacao = EscolaSituacao.Ativa) : base(tenantId, entidadeId)
    {
        Codigo = Require(codigo, nameof(codigo));
        Nome = Require(nome, nameof(nome));
        Tipo = tipo;
        Situacao = situacao;
    }

    public Escola(string nome) : this(1, 1, "ESC-DEFAULT", nome) { }
    public string Codigo { get; }
    public string Nome { get; private set; }
    public EscolaTipo Tipo { get; }
    public EscolaSituacao Situacao { get; private set; }
}

public sealed class AnoLetivo : EducacaoEntity
{
    public AnoLetivo(long tenantId, long entidadeId, long exercicioId, int ano, DateOnly dataInicio, DateOnly dataFim, long? escolaId = null) : base(tenantId, entidadeId, exercicioId)
    {
        if (dataFim < dataInicio) throw new InvalidOperationException("Data final do ano letivo não pode ser anterior à inicial.");
        if (ano < 1900) throw new ArgumentException("Ano letivo inválido.", nameof(ano));
        Ano = ano; DataInicio = dataInicio; DataFim = dataFim; EscolaId = escolaId; Status = AnoLetivoStatus.Planejado;
    }

    public int Ano { get; }
    public DateOnly DataInicio { get; }
    public DateOnly DataFim { get; }
    public long? EscolaId { get; }
    public AnoLetivoStatus Status { get; private set; }
    public void Encerrar() => Status = AnoLetivoStatus.Encerrado;
}

public sealed class Curso : EducacaoEntity
{
    public Curso(long tenantId, long entidadeId, string codigo, string nome, EtapaEnsino etapa) : base(tenantId, entidadeId)
    { Codigo = Require(codigo, nameof(codigo)); Nome = Require(nome, nameof(nome)); Etapa = etapa; }
    public string Codigo { get; }
    public string Nome { get; }
    public EtapaEnsino Etapa { get; }
}

public sealed class SerieAno : EducacaoEntity
{
    public SerieAno(long tenantId, long entidadeId, long cursoId, string codigo, string nome, int ordem) : base(tenantId, entidadeId)
    { CursoId = RequireId(cursoId, nameof(cursoId)); Codigo = Require(codigo, nameof(codigo)); Nome = Require(nome, nameof(nome)); Ordem = ordem > 0 ? ordem : throw new ArgumentException("Ordem deve ser positiva.", nameof(ordem)); }
    public long CursoId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public int Ordem { get; }
}

public sealed class Turma : EducacaoEntity
{
    public Turma(long tenantId, long entidadeId, long exercicioId, long escolaId, long anoLetivoId, long cursoId, long serieAnoId, string codigo, string nome, Turno turno, int capacidade) : base(tenantId, entidadeId, exercicioId)
    {
        EscolaId = RequireId(escolaId, nameof(escolaId)); AnoLetivoId = RequireId(anoLetivoId, nameof(anoLetivoId)); CursoId = RequireId(cursoId, nameof(cursoId)); SerieAnoId = RequireId(serieAnoId, nameof(serieAnoId));
        Codigo = Require(codigo, nameof(codigo)); Nome = Require(nome, nameof(nome)); Turno = turno; Capacidade = capacidade > 0 ? capacidade : throw new InvalidOperationException("Capacidade da turma deve ser maior que zero."); Status = TurmaStatus.Planejada;
    }
    public long EscolaId { get; }
    public long AnoLetivoId { get; }
    public long CursoId { get; }
    public long SerieAnoId { get; }
    public string Codigo { get; }
    public string Nome { get; }
    public Turno Turno { get; }
    public int Capacidade { get; }
    public int VagasOcupadas { get; private set; }
    public TurmaStatus Status { get; private set; }
    public void MatricularAluno() { if (VagasOcupadas >= Capacidade) throw new InvalidOperationException("Turma não possui vagas disponíveis."); VagasOcupadas++; }
    public void CancelarMatricula() { if (VagasOcupadas > 0) VagasOcupadas--; }
}

public sealed class Aluno : EducacaoEntity
{
    public Aluno(long tenantId, long entidadeId, long pessoaId, string codigoAluno) : base(tenantId, entidadeId)
    { PessoaId = RequireId(pessoaId, nameof(pessoaId)); CodigoAluno = Require(codigoAluno, nameof(codigoAluno)); Situacao = AlunoSituacao.Ativo; }
    public long PessoaId { get; }
    public string CodigoAluno { get; }
    public string? Nis { get; private set; }
    public string? CartaoSus { get; private set; }
    public bool NecessidadeEspecial { get; private set; }
    public string DadosSensiveisProtegidos => "***";
    public AlunoSituacao Situacao { get; }
}

public sealed class ResponsavelAluno : EducacaoEntity
{
    public ResponsavelAluno(long tenantId, long entidadeId, long alunoId, long pessoaId, string parentesco) : base(tenantId, entidadeId)
    { AlunoId = RequireId(alunoId, nameof(alunoId)); PessoaId = RequireId(pessoaId, nameof(pessoaId)); Parentesco = Require(parentesco, nameof(parentesco)); }
    public long AlunoId { get; }
    public long PessoaId { get; }
    public string Parentesco { get; }
}

public sealed class Matricula : EducacaoEntity
{
    public Matricula(long tenantId, long entidadeId, long exercicioId, long alunoId, long escolaId, long anoLetivoId, Turma turma, string numero) : base(tenantId, entidadeId, exercicioId)
    {
        AlunoId = RequireId(alunoId, nameof(alunoId)); EscolaId = RequireId(escolaId, nameof(escolaId)); AnoLetivoId = RequireId(anoLetivoId, nameof(anoLetivoId)); TurmaId = RequireId(turma.Id == 0 ? 1 : turma.Id, nameof(turma)); Numero = Require(numero, nameof(numero));
        turma.MatricularAluno(); Status = MatriculaStatus.Ativa;
    }
    public long AlunoId { get; }
    public long EscolaId { get; }
    public long AnoLetivoId { get; }
    public long TurmaId { get; }
    public string Numero { get; }
    public MatriculaStatus Status { get; private set; }
    public bool PodeReceberFrequencia => Status != MatriculaStatus.Cancelada;
    public void Cancelar(Turma? turma = null) { if (Status == MatriculaStatus.Cancelada) return; Status = MatriculaStatus.Cancelada; turma?.CancelarMatricula(); }
    public static void GarantirSemMatriculaAtivaDuplicada(IEnumerable<Matricula> matriculas, long alunoId, long escolaId, long anoLetivoId)
    { if (matriculas.Any(m => m.AlunoId == alunoId && m.EscolaId == escolaId && m.AnoLetivoId == anoLetivoId && m.Status == MatriculaStatus.Ativa)) throw new InvalidOperationException("Aluno já possui matrícula ativa na escola e ano letivo informados."); }
}

public sealed class Professor : EducacaoEntity
{
    public Professor(long tenantId, long entidadeId, long pessoaId, string codigoProfessor) : base(tenantId, entidadeId)
    { PessoaId = RequireId(pessoaId, nameof(pessoaId)); CodigoProfessor = Require(codigoProfessor, nameof(codigoProfessor)); Situacao = ProfessorSituacao.Ativo; }
    public long PessoaId { get; }
    public string CodigoProfessor { get; }
    public ProfessorSituacao Situacao { get; }
}

public sealed class ProfessorTurma : EducacaoEntity
{
    public ProfessorTurma(long tenantId, long entidadeId, long exercicioId, long professorId, long turmaId, string componenteCurricular) : base(tenantId, entidadeId, exercicioId)
    { ProfessorId = RequireId(professorId, nameof(professorId)); TurmaId = RequireId(turmaId, nameof(turmaId)); ComponenteCurricular = Require(componenteCurricular, nameof(componenteCurricular)); }
    public long ProfessorId { get; }
    public long TurmaId { get; }
    public string ComponenteCurricular { get; }
}

public sealed class DiarioFrequencia : EducacaoEntity
{
    public DiarioFrequencia(long tenantId, long entidadeId, long exercicioId, Matricula matricula, long turmaId, long alunoId, DateOnly dataAula, bool presente) : base(tenantId, entidadeId, exercicioId)
    { if (!matricula.PodeReceberFrequencia) throw new InvalidOperationException("Matrícula cancelada não pode receber frequência."); if (matricula.TurmaId != turmaId || matricula.AlunoId != alunoId) throw new InvalidOperationException("Frequência exige aluno matriculado na turma."); TurmaId = turmaId; AlunoId = alunoId; DataAula = dataAula; Presente = presente; }
    public long TurmaId { get; }
    public long AlunoId { get; }
    public DateOnly DataAula { get; }
    public bool Presente { get; }
}

public sealed class Avaliacao : EducacaoEntity
{
    public Avaliacao(long tenantId, long entidadeId, long exercicioId, long turmaId, string componenteCurricular, string titulo, DateOnly data, decimal valorMaximo = 10m) : base(tenantId, entidadeId, exercicioId)
    { TurmaId = RequireId(turmaId, nameof(turmaId)); ComponenteCurricular = Require(componenteCurricular, nameof(componenteCurricular)); Titulo = Require(titulo, nameof(titulo)); Data = data; ValorMaximo = valorMaximo > 0m ? valorMaximo : throw new InvalidOperationException("Valor máximo deve ser positivo."); Status = AvaliacaoStatus.Aberta; }
    public long TurmaId { get; }
    public string ComponenteCurricular { get; }
    public string Titulo { get; }
    public DateOnly Data { get; }
    public decimal ValorMaximo { get; }
    public AvaliacaoStatus Status { get; }
}

public sealed class Nota : EducacaoEntity
{
    public Nota(long tenantId, long entidadeId, long exercicioId, Avaliacao avaliacao, long alunoId, decimal valor) : base(tenantId, entidadeId, exercicioId)
    { AvaliacaoId = RequireId(avaliacao.TurmaId == 0 ? 1 : avaliacao.TurmaId, nameof(avaliacao)); AlunoId = RequireId(alunoId, nameof(alunoId)); if (valor < 0m) throw new InvalidOperationException("Nota não pode ser negativa."); if (valor > avaliacao.ValorMaximo) throw new InvalidOperationException("Nota não pode ultrapassar o valor máximo da avaliação."); Valor = valor; }
    public long AvaliacaoId { get; }
    public long AlunoId { get; }
    public decimal Valor { get; }
}

public sealed class PreMatriculaInscricao : EducacaoEntity
{
    public PreMatriculaInscricao(long tenantId, long entidadeId, long exercicioId, long alunoPessoaId, string protocolo, int anoLetivo, EtapaEnsino etapaEnsino) : base(tenantId, entidadeId, exercicioId)
    { AlunoPessoaId = RequireId(alunoPessoaId, nameof(alunoPessoaId)); Protocolo = Require(protocolo, nameof(protocolo)); AnoLetivo = anoLetivo; EtapaEnsino = etapaEnsino; Status = PreMatriculaStatus.Recebida; }
    public long AlunoPessoaId { get; }
    public string Protocolo { get; }
    public int AnoLetivo { get; }
    public EtapaEnsino EtapaEnsino { get; }
    public PreMatriculaStatus Status { get; private set; }
    public void Converter() { if (Status == PreMatriculaStatus.ConvertidaMatricula) throw new InvalidOperationException("Pré-matrícula convertida não pode ser convertida novamente."); Status = PreMatriculaStatus.ConvertidaMatricula; }
}

public sealed class EducacensoRegistro : EducacaoEntity
{
    public EducacensoRegistro(long tenantId, long entidadeId, long exercicioId, string tipoRegistro, string payload) : base(tenantId, entidadeId, exercicioId)
    { TipoRegistro = Require(tipoRegistro, nameof(tipoRegistro)); Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload; Status = EducacensoStatus.Pendente; }
    public string TipoRegistro { get; }
    public string Payload { get; }
    public EducacensoStatus Status { get; }
}

public sealed class PortalEducacaoAcesso : EducacaoEntity
{
    public PortalEducacaoAcesso(long tenantId, long entidadeId, long pessoaId, string acao) : base(tenantId, entidadeId)
    { PessoaId = RequireId(pessoaId, nameof(pessoaId)); Acao = Require(acao, nameof(acao)); }
    public long PessoaId { get; }
    public string Acao { get; }
}

public sealed class EducacaoEvento : EducacaoEntity
{
    public EducacaoEvento(long tenantId, long entidadeId, string tipo, string payload) : base(tenantId, entidadeId)
    { Tipo = Require(tipo, nameof(tipo)); Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload; }
    public string Tipo { get; }
    public string Payload { get; }
}
