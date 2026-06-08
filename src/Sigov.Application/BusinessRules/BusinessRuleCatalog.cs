namespace Sigov.Application.BusinessRules;

public interface IBusinessRuleCatalog
{
    IReadOnlyList<IBusinessRule> GetRules();

    IReadOnlyList<IBusinessRule> GetRulesByModule(string module);
}

public sealed class BusinessRuleCatalog : IBusinessRuleCatalog
{
    private static readonly IReadOnlyList<IBusinessRule> Rules = BuildRules();

    public IReadOnlyList<IBusinessRule> GetRules() => Rules;

    public IReadOnlyList<IBusinessRule> GetRulesByModule(string module)
    {
        var normalized = NormalizeModuleName(module);
        return Rules
            .Where(rule => string.Equals(rule.Module, normalized, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }


    private static string NormalizeModuleName(string module)
    {
        return module.Trim().ToUpperInvariant() switch
        {
            "CORE" or "PESSOAS" => "Core/Pessoas",
            "CADASTROS" or "ENTIDADES" or "EXERCICIOS" or "EXERCÍCIOS" or "UNIDADES" => "Core/Cadastros",
            "SECURITY" or "SEGURANCA" or "SEGURANÇA" => "Segurança",
            "AUDIT" or "AUDITORIA" => "Auditoria",
            "LGPD" => "LGPD",
            "SUPORTE" or "OPERACAO" or "OPERAÇÃO" => "Suporte/Operação",
            "BI" or "RELATORIOS" or "RELATÓRIOS" => "Relatórios/BI",
            "PROCESSOS" or "PROTOCOLOS" or "OUVIDORIA" or "DIARIO" or "DIÁRIO" => "Processos",
            "FINANCEIRO" or "SIAFIC" or "ORCAMENTO" or "ORÇAMENTO" => "Financeiro",
            "TRIBUTARIO" or "TRIBUTÁRIO" or "TRIBUTOS" => "Tributário",
            "RH" or "RECURSOS HUMANOS" or "FOLHA" or "PONTO" => "RH",
            "EDUCACAO" or "EDUCAÇÃO" or "ESCOLAS" or "ENSINO" => "Educação",
            _ => module
        };
    }

    private static IReadOnlyList<IBusinessRule> BuildRules()
    {
        var data = new Dictionary<string, string[]>
        {
            ["Core/Pessoas"] = new[] { "Pessoa exige nome.", "Pessoa física exige CPF válido ou regra configurada.", "Pessoa jurídica exige CNPJ válido ou regra configurada.", "Documento CPF/CNPJ deve ser normalizado.", "Documento duplicado por tenant/entidade deve ser bloqueado.", "Pessoa excluída não aparece em listagens padrão.", "Consulta de pessoa registra acesso a dado pessoal.", "Endereço principal deve ser único por pessoa quando aplicável.", "Contato principal deve ser único por tipo quando aplicável." },
            ["Core/Cadastros"] = new[] { "Entidade exige nome e tenant.", "CNPJ é obrigatório quando aplicável.", "Entidade em uso não pode ser excluída sem regra clara.", "Exercício exige ano.", "Exercício deve ser único por entidade.", "Exercício encerrado bloqueia operações operacionais.", "Unidade exige nome.", "Unidade inativa não aparece em selects operacionais.", "Unidade excluída usa soft delete." },
            ["Segurança"] = new[] { "Não excluir último administrador.", "Não bloquear o único administrador ativo.", "Não remover a própria permissão crítica se for único admin.", "Login duplicado por tenant bloqueia.", "E-mail duplicado por tenant bloqueia quando a regra estiver habilitada.", "Senha deve respeitar política.", "Senha default é proibida em Production.", "Reset de senha audita.", "Reset de senha não retorna senha em Production.", "Senha temporária somente Development/Homologation.", "Login inválido incrementa tentativas.", "Usuário bloqueado não autentica.", "Permissão crítica exige auditoria.", "Alteração de perfil/permissão audita.", "Bloqueio/desbloqueio audita." },
            ["SaaS"] = new[] { "Tenant suspenso bloqueia operação.", "Tenant cancelado bloqueia login comum.", "Tenant cancelado bloqueia operação.", "Módulo não contratado retorna 403.", "Módulo não contratado bloqueia endpoint.", "Feature desabilitada bloqueia recurso.", "Feature flag desativada bloqueia recurso.", "Tenant não pode acessar dados de outro tenant.", "Somente SIGOV_ADMIN acessa SaaS Admin.", "Tenant admin não acessa painel global.", "Suspender, reativar ou cancelar tenant audita.", "Alterar plano audita.", "Alterar módulos contratados audita.", "Alterar feature flags audita.", "Uso mensal não deve misturar tenants." },
            ["Auditoria"] = new[] { "Operações críticas geram trilha.", "Alteração guarda antes/depois.", "Consulta de dado pessoal gera registro.", "Dados sensíveis não aparecem completos para usuário sem permissão.", "Auditoria exige permissão.", "Paginação é obrigatória em listagens de auditoria.", "Stack trace nunca é exibido ao usuário final." },
            ["LGPD"] = new[] { "Solicitação do titular tem prazo.", "Solicitação do titular recebe número.", "Solicitação respondida registra usuário e data.", "Consentimento revogado não pode ser usado como base ativa.", "Consentimento pode ser revogado.", "Incidente de segurança exige severidade.", "Incidente de segurança exige descrição.", "Relatório do titular deve auditar geração.", "Consulta de relatório registra acesso a dado pessoal.", "Retenção/descarte exige justificativa.", "Dados sensíveis devem ser mascarados quando necessário." },
            ["Processos"] = new[] { "Processo encerrado não movimenta.", "Processo cancelado não movimenta.", "Processo sigiloso exige permissão específica.", "Movimentação exige despacho.", "Parecer exige texto.", "Parecer sigiloso exige permissão.", "Protocolo convertido não converte novamente.", "Ouvidoria anônima não exige pessoa.", "Diário publicado não edita sem permissão administrativa.", "Consulta de interessado registra acesso a dado pessoal." },
            ["Financeiro"] = new[] { "Orçamento de despesa não aceita dotação negativa.", "Orçamento de receita não aceita previsão negativa.", "Empenho não pode ultrapassar saldo disponível.", "Liquidação não pode ultrapassar saldo do empenho.", "Pagamento não pode ultrapassar saldo liquidado.", "Receita arrecadada não ultrapassa lançada.", "Exercício encerrado bloqueia operações.", "Dinheiro usa decimal/numeric; double e float são proibidos.", "Operação financeira audita.", "Anulação respeita valores já liquidados/pagos." },
            ["Tributário"] = new[] { "Contribuinte exige pessoa.", "Inscrição municipal é única por tenant e entidade.", "Lançamento exige valor positivo.", "Parcelas fecham com total do lançamento.", "Parcela paga não gera novo DAM.", "DAM fake bloqueado em Production.", "PIX dev bloqueado em Production.", "Pagamento dev bloqueado em Production.", "Certidão negativa só sem débito vencido.", "Dívida ativa só parcela vencida não paga.", "Carnê finaliza apenas com itens.", "Consulta de contribuinte audita dado pessoal." },
            ["Compras"] = new[] { "Solicitação exige item.", "Contrato exige vigência válida.", "Medição não ultrapassa saldo do contrato.", "Estoque não permite saída sem saldo.", "Bem baixado não movimenta." },
            ["RH"] = new[] { "Servidor exige pessoa e matrícula.", "Matrícula de servidor é única por tenant e entidade.", "Servidor inativo não aparece em novas folhas e ponto, salvo filtro explícito.", "Consulta de servidor audita acesso a dado pessoal.", "Dados de servidor são pessoais e mascarados por padrão.", "Cargo exige código e nome.", "Código de cargo é único por tenant e entidade.", "Lotação exige nome.", "Vínculo exige servidor e tipo.", "Encerrar vínculo exige data e justificativa.", "Folha exige competência válida.", "Folha mês entre 1 e 13.", "Folha fechada não recebe lançamento comum.", "Folha integrada ao financeiro não altera sem permissão administrativa.", "Lançamento de folha não aceita valor negativo.", "Total bruto, descontos e líquido da folha são calculados no backend.", "Integração RH Financeiro gera outbox e exige histórico.", "Fechamento e integração de folha auditam antes/depois.", "Registro de ponto exige servidor, data/hora e tipo.", "Ponto não permite duplicidade exata de marcação.", "Ajuste de ponto exige justificativa.", "Ponto aprovado não altera sem permissão.", "Férias fim >= início.", "Férias não conflitam com afastamento ativo.", "Aprovação ou cancelamento de férias exige usuário e justificativa quando aplicável.", "Afastamento fim >= início.", "Motivo de afastamento sensível deve ser mascarado.", "Saúde ocupacional é dado sensível.", "Resultado de exame ocupacional exige permissão específica.", "Evento eSocial estrutural exige tipo e payload JSON válido.", "Adapter eSocial dev é bloqueado em Production.", "Portal do servidor só mostra dados autorizados.", "Contracheque, afastamento e saúde ocupacional auditam acesso a dado pessoal." },
            ["Educação"] = new[] { "Escola exige código e nome.", "Código de escola é único por tenant e entidade.", "INEP informado deve ser normalizado.", "Escola inativa não aparece em novas turmas ou matrículas.", "Ano letivo fim >= início.", "Ano letivo encerrado bloqueia novas matrículas.", "Curso exige código e nome.", "Série exige curso e ordem positiva.", "Turma exige escola, ano letivo, curso e série.", "Turma exige capacidade positiva.", "Vagas ocupadas não ultrapassam capacidade.", "Turma fechada não recebe matrícula.", "Turma cancelada não recebe frequência.", "Aluno exige pessoa.", "Código do aluno é único por tenant e entidade.", "Dados de aluno e responsável são dados pessoais.", "Dados sensíveis do aluno devem ser mascarados.", "NIS e Cartão SUS são mascarados por padrão.", "Consulta de aluno audita acesso a dado pessoal.", "Responsável exige pessoa.", "Responsável legal e contato de emergência devem ser destacados.", "Matrícula exige aluno, turma, escola e ano letivo.", "Aluno não deve ter matrícula ativa duplicada na mesma escola/ano.", "Matrícula ativa ocupa vaga.", "Matrícula cancelada libera vaga.", "Transferência de matrícula atualiza turma e audita.", "Matrícula cancelada não recebe frequência.", "Turma sem vaga bloqueia matrícula.", "Professor exige pessoa.", "Professor inativo não aparece em novas turmas.", "Vínculo professor-turma exige componente curricular.", "Frequência exige turma e matrícula ativa.", "Frequência não pode ser lançada para matrícula cancelada.", "Data da aula pertence ao ano letivo.", "Alteração de frequência audita.", "Avaliação exige turma e componente curricular.", "Valor máximo da avaliação deve ser maior que zero.", "Nota não pode ser negativa.", "Nota não pode ultrapassar valor máximo.", "Avaliação fechada não recebe nota sem permissão.", "Alteração de nota audita.", "Pré-matrícula exige aluno ou pessoa.", "Pré-matrícula convertida não converte novamente.", "Indeferimento de pré-matrícula exige justificativa.", "Conversão de pré-matrícula cria matrícula se houver vaga e audita.", "Educacenso estrutural exige tipo e payload JSON válido.", "Adapter Educacenso dev é bloqueado em Production.", "Portal Pais/Alunos só mostra alunos vinculados e audita acesso." },
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
