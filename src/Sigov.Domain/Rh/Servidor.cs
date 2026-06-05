using Sigov.Domain.Common;

namespace Sigov.Domain.Rh;

public enum VinculoTipo { Efetivo, Comissionado, Temporario, Estagiario, Terceirizado }
public enum FolhaStatus { Aberta, Calculada, IntegradaFinanceiro, Fechada, Cancelada }
public enum FolhaEventoTipo { Provento, Desconto, Informativo }
public enum PontoTipo { Entrada, Saida, IntervaloInicio, IntervaloFim, Ajuste }
public enum FeriasStatus { Programada, Aprovada, EmGozo, Concluida, Cancelada }
public enum AfastamentoStatus { Solicitado, Aprovado, EmCurso, Encerrado, Indeferido }
public enum EsocialStatus { Pendente, Validado, Enviado, Retificado, Erro }

public abstract class RhEntity : AggregateRoot, ITenantScopedEntity, ISoftDelete, IAuditableEntity
{
    protected RhEntity(long tenantId)
    {
        if (tenantId <= 0) throw new ArgumentException("Tenant obrigatório.", nameof(tenantId));
        TenantId = tenantId;
    }

    public long TenantId { get; private set; }
    public long? EntidadeId => null;
    public bool Ativo { get; protected set; } = true;
    public bool IsDeleted { get; protected set; }
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public long? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public long? UpdatedBy { get; protected set; }
    public DateTimeOffset? DeletedAt { get; protected set; }
    public long? DeletedBy { get; protected set; }

    public void Excluir(long? usuarioId)
    {
        IsDeleted = true;
        Ativo = false;
        UpdatedBy = usuarioId;
        UpdatedAt = DateTimeOffset.UtcNow;
        DeletedBy = usuarioId;
        DeletedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class Servidor : RhEntity
{
    public Servidor(long tenantId, string matricula, string nome, string cpf, DateOnly dataNascimento, long? pessoaId = null) : base(tenantId)
    {
        Matricula = Require(matricula, nameof(matricula));
        Nome = Require(nome, nameof(nome));
        Cpf = Require(cpf, nameof(cpf));
        DataNascimento = dataNascimento;
        PessoaId = pessoaId;
    }

    public long? PessoaId { get; private set; }
    public string Matricula { get; private set; }
    public string Nome { get; private set; }
    public string Cpf { get; private set; }
    public DateOnly DataNascimento { get; private set; }
    public string? EmailInstitucional { get; private set; }
    public string? Telefone { get; private set; }
    public string ClassificacaoLgpd { get; private set; } = "dados_pessoais_sensiveis";

    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Campo obrigatório.", name) : value.Trim();
}

public sealed class Cargo : RhEntity { public Cargo(long tenantId, string codigo, string nome) : base(tenantId) { Codigo = Req(codigo); Nome = Req(nome); } public string Codigo { get; } public string Nome { get; } public string? Cbo { get; init; } public decimal VencimentoBase { get; init; } private static string Req(string v)=>string.IsNullOrWhiteSpace(v)?throw new ArgumentException("Campo obrigatório."):v.Trim(); }
public sealed class Lotacao : RhEntity { public Lotacao(long tenantId, string codigo, string nome) : base(tenantId) { Codigo = Req(codigo); Nome = Req(nome); } public string Codigo { get; } public string Nome { get; } public long? LotacaoPaiId { get; init; } private static string Req(string v)=>string.IsNullOrWhiteSpace(v)?throw new ArgumentException("Campo obrigatório."):v.Trim(); }
public sealed class Vinculo : RhEntity { public Vinculo(long tenantId, long servidorId, long cargoId, long lotacaoId, VinculoTipo tipo, DateOnly admissao) : base(tenantId) { ServidorId = servidorId; CargoId = cargoId; LotacaoId = lotacaoId; Tipo = tipo; DataAdmissao = admissao; } public long ServidorId { get; } public long CargoId { get; } public long LotacaoId { get; } public VinculoTipo Tipo { get; } public DateOnly DataAdmissao { get; } public DateOnly? DataDesligamento { get; init; } }
public sealed class Folha : RhEntity { public Folha(long tenantId, int ano, int mes, string tipo) : base(tenantId) { Ano = ano; Mes = mes is >= 1 and <= 13 ? mes : throw new ArgumentException("Mês inválido."); Tipo = string.IsNullOrWhiteSpace(tipo) ? "mensal" : tipo.Trim(); } public int Ano { get; } public int Mes { get; } public string Tipo { get; } public FolhaStatus Status { get; private set; } = FolhaStatus.Aberta; }
public sealed class FolhaEvento : RhEntity { public FolhaEvento(long tenantId, string codigo, string descricao, FolhaEventoTipo tipo) : base(tenantId) { Codigo = codigo; Descricao = descricao; Tipo = tipo; } public string Codigo { get; } public string Descricao { get; } public FolhaEventoTipo Tipo { get; } public bool IncideInss { get; init; } public bool IncideIrrf { get; init; } }
public sealed class FolhaLancamento : RhEntity { public FolhaLancamento(long tenantId, long folhaId, long servidorId, long eventoId, decimal valor) : base(tenantId) { FolhaId = folhaId; ServidorId = servidorId; EventoId = eventoId; Valor = valor; } public long FolhaId { get; } public long ServidorId { get; } public long EventoId { get; } public decimal Valor { get; } public long? EmpenhoId { get; init; } }
public sealed class Ponto : RhEntity { public Ponto(long tenantId, long servidorId, DateTimeOffset dataHora, PontoTipo tipo) : base(tenantId) { ServidorId = servidorId; DataHora = dataHora; Tipo = tipo; } public long ServidorId { get; } public DateTimeOffset DataHora { get; } public PontoTipo Tipo { get; } public string? Origem { get; init; } }
public sealed class Ferias : RhEntity { public Ferias(long tenantId, long servidorId, DateOnly inicio, DateOnly fim) : base(tenantId) { ServidorId = servidorId; Inicio = inicio; Fim = fim; } public long ServidorId { get; } public DateOnly Inicio { get; } public DateOnly Fim { get; } public FeriasStatus Status { get; init; } = FeriasStatus.Programada; }
public sealed class Afastamento : RhEntity { public Afastamento(long tenantId, long servidorId, string motivo, DateOnly inicio, DateOnly? fim) : base(tenantId) { ServidorId = servidorId; Motivo = motivo; Inicio = inicio; Fim = fim; } public long ServidorId { get; } public string Motivo { get; } public DateOnly Inicio { get; } public DateOnly? Fim { get; } public AfastamentoStatus Status { get; init; } = AfastamentoStatus.Solicitado; }
public sealed class SaudeOcupacional : RhEntity { public SaudeOcupacional(long tenantId, long servidorId, string tipo, DateOnly data) : base(tenantId) { ServidorId = servidorId; Tipo = tipo; Data = data; } public long ServidorId { get; } public string Tipo { get; } public DateOnly Data { get; } public DateOnly? Validade { get; init; } public string? Resultado { get; init; } }
public sealed class eSocial : RhEntity { public eSocial(long tenantId, string evento, EsocialStatus status) : base(tenantId) { Evento = evento; Status = status; } public string Evento { get; } public long? ServidorId { get; init; } public EsocialStatus Status { get; } public string? Recibo { get; init; } }
public sealed class PortalUsuario : RhEntity { public PortalUsuario(long tenantId, long servidorId, string login) : base(tenantId) { ServidorId = servidorId; Login = login; } public long ServidorId { get; } public string Login { get; } }
public sealed class PortalAcesso : RhEntity { public PortalAcesso(long tenantId, long portalUsuarioId, DateTimeOffset acessadoEm) : base(tenantId) { PortalUsuarioId = portalUsuarioId; AcessadoEm = acessadoEm; } public long PortalUsuarioId { get; } public DateTimeOffset AcessadoEm { get; } public string? Ip { get; init; } }
public sealed class RhEvento : RhEntity { public RhEvento(long tenantId, string tipo, string agregado, long agregadoId) : base(tenantId) { Tipo = tipo; Agregado = agregado; AgregadoId = agregadoId; } public string Tipo { get; } public string Agregado { get; } public long AgregadoId { get; } public bool Publicado { get; init; } public string PayloadJson { get; init; } = "{}"; }
