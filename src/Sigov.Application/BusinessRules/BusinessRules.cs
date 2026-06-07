namespace Sigov.Application.BusinessRules;

public enum BusinessRuleSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public sealed record BusinessRuleContext(long? TenantId, long? UserId, string Module, IReadOnlyDictionary<string, object?> Values);

public sealed record BusinessRuleViolation(string Code, string Message, BusinessRuleSeverity Severity, string Module);

public sealed record BusinessRuleResult(bool IsValid, IReadOnlyList<BusinessRuleViolation> Violations)
{
    public static BusinessRuleResult Success { get; } = new(true, Array.Empty<BusinessRuleViolation>());
}

public interface IBusinessRule
{
    string Code { get; }

    string Module { get; }

    string Description { get; }

    BusinessRuleSeverity Severity { get; }
}

public sealed record BusinessRuleDefinition(string Code, string Module, string Description, BusinessRuleSeverity Severity) : IBusinessRule;

public interface IBusinessRuleEvaluator
{
    BusinessRuleResult Evaluate(BusinessRuleContext context, IEnumerable<IBusinessRule> rules);
}

public sealed class BusinessRuleEvaluator : IBusinessRuleEvaluator
{
    public BusinessRuleResult Evaluate(BusinessRuleContext context, IEnumerable<IBusinessRule> rules)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);

        var violations = rules
            .Where(rule => rule.Severity is BusinessRuleSeverity.Error or BusinessRuleSeverity.Critical)
            .Select(rule => new BusinessRuleViolation(rule.Code, rule.Description, rule.Severity, rule.Module))
            .ToArray();

        return new BusinessRuleResult(violations.Length == 0, violations);
    }
}

public interface IBusinessRuleCatalog
{
    IReadOnlyList<IBusinessRule> GetRules();

    IReadOnlyList<IBusinessRule> GetRulesByModule(string module);
}

public sealed class BusinessRuleCatalog : IBusinessRuleCatalog
{
    private static readonly IReadOnlyList<IBusinessRule> Rules = BuildRules();

    public IReadOnlyList<IBusinessRule> GetRules() => Rules;

    public IReadOnlyList<IBusinessRule> GetRulesByModule(string module) => Rules
        .Where(rule => string.Equals(rule.Module, module, StringComparison.OrdinalIgnoreCase))
        .ToArray();

    private static IReadOnlyList<IBusinessRule> BuildRules()
    {
        var data = new Dictionary<string, string[]>
        {
            ["Core/Pessoas"] = new[] { "Documento CPF/CNPJ deve ser normalizado.", "Documento duplicado por tenant/entidade deve ser bloqueado.", "Pessoa excluída não aparece em listagens padrão.", "Consulta de pessoa registra acesso a dado pessoal." },
            ["Segurança"] = new[] { "Não excluir último administrador.", "Senha deve respeitar política.", "Login inválido incrementa tentativas.", "Usuário bloqueado não autentica.", "Permissão crítica exige auditoria." },
            ["SaaS"] = new[] { "Tenant suspenso bloqueia operação.", "Tenant cancelado bloqueia login.", "Módulo não contratado retorna 403.", "Feature desabilitada bloqueia recurso.", "Tenant não pode acessar dados de outro tenant." },
            ["Processos"] = new[] { "Processo encerrado não movimenta.", "Processo sigiloso exige permissão específica.", "Movimentação exige despacho.", "Parecer sigiloso exige permissão.", "Protocolo convertido não converte novamente." },
            ["Financeiro"] = new[] { "Empenho não pode ultrapassar saldo.", "Liquidação não pode ultrapassar saldo do empenho.", "Pagamento não pode ultrapassar liquidado.", "Exercício encerrado bloqueia lançamento.", "Anulação respeita valores já liquidados/pagos." },
            ["Tributário"] = new[] { "Lançamento deve ter valor maior que zero.", "Parcelas devem fechar com total.", "DAM fake apenas Development.", "PIX dev apenas Development.", "Certidão negativa só sem débito vencido.", "Dívida ativa só parcela vencida e não paga." },
            ["Compras"] = new[] { "Solicitação exige item.", "Contrato exige vigência válida.", "Medição não ultrapassa saldo do contrato.", "Estoque não permite saída sem saldo.", "Bem baixado não movimenta." },
            ["RH"] = new[] { "Servidor exige matrícula e CPF.", "Folha mês entre 1 e 13.", "Lançamento não aceita valor negativo.", "Férias fim >= início.", "Exercício encerrado bloqueia folha/ponto." },
            ["Educação"] = new[] { "Turma não pode exceder vagas.", "Aluno não deve ter matrícula ativa duplicada na mesma escola/ano.", "Frequência exige matrícula ativa.", "Nota não pode ultrapassar valor máximo.", "Pré-matrícula convertida não converte novamente." },
            ["Saúde"] = new[] { "Paciente exige pessoa.", "Prontuário é dado sensível.", "Dispensação não pode deixar estoque negativo.", "Visita ACS exige paciente/domicílio/indivíduo.", "Dados clínicos sempre sensíveis." },
            ["Saneamento"] = new[] { "Leitura atual não pode ser menor que anterior sem ajuste.", "Fatura paga não recebe novo pagamento.", "Pagamento não ultrapassa saldo.", "Ordem cancelada não executa.", "Coordenadas devem ser válidas." },
            ["Social"] = new[] { "Família tem no máximo um responsável ativo.", "Atendimento exige demanda.", "Benefício concedido exige autorização.", "Parecer social é sensível.", "Vulnerabilidade exige família ou pessoa." },
            ["Relatórios/BI"] = new[] { "Fonte SQL precisa ser aprovada.", "SQL perigoso é bloqueado.", "Exportação com dado pessoal audita.", "Dataset público com dado pessoal exige anonimização.", "Tenant privado não vaza para público." },
            ["Integrações"] = new[] { "API key nunca em texto puro.", "Webhook deve validar assinatura quando configurada.", "Idempotency impede duplicidade.", "Outbox excedendo tentativas vira dead-letter.", "Adapter fake bloqueado em Production." },
            ["Suporte/Operação"] = new[] { "Chamado exige assunto e descrição.", "SLA calcula prazos.", "Satisfação apenas chamado resolvido/encerrado.", "Restore exige confirmação.", "Health não vaza stack trace." }
        };

        return data.SelectMany(pair => pair.Value.Select((description, index) =>
                new BusinessRuleDefinition($"{Normalize(pair.Key)}-{index + 1:00}", pair.Key, description, BusinessRuleSeverity.Error)))
            .ToArray();
    }

    private static string Normalize(string value)
    {
        return value.Replace("/", "-", StringComparison.Ordinal).Replace(" ", "-", StringComparison.Ordinal).ToUpperInvariant();
    }
}
