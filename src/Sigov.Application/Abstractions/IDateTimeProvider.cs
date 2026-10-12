namespace Sigov.Application.Abstractions;

/// <summary>
/// Fonte única de tempo do sistema (Ciclo 2 Sprint 2 — BR-2.5): relógio operacional centralizado
/// em escopo DI e configurável. <see cref="UtcNow"/> expõe o instante UTC (quando a camada precisa
/// de instante); <see cref="DataAtual"/> expõe a DATA OPERACIONAL (data local do host), usada pelas
/// regras de negócio exercidas em C# (elegibilidades, prazos, atribuição de registros). O SQL segue
/// usando now()/current_date somente onde o banco exerce a regra (auditoria, updated_at, competência).
/// Configurável: o ambiente pode fixar a data operacional (SIGOV_CLOCK_DATAFIXA, yyyy-MM-dd) para
/// desenvolvimento/homologação — trocar a fonte NÃO altera o resultado de elegibilidade dentro do
/// mesmo dia operacional.
/// </summary>
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Data operacional para regras de negócio (data local do host; fixada se SIGOV_CLOCK_DATAFIXA estiver definida).</summary>
    DateOnly DataAtual { get; }
}
