using Sigov.Web.Models.Operational;

namespace Sigov.Web.Services;

public sealed class OperationalDemoService
{
    private readonly IDatabaseSchemaInspector _schemaInspector;
    private readonly ILogger<OperationalDemoService> _logger;

    public OperationalDemoService(IDatabaseSchemaInspector schemaInspector, ILogger<OperationalDemoService> logger)
    {
        _schemaInspector = schemaInspector;
        _logger = logger;
    }
    private sealed record OperationalModuleSeed(
        string Area,
        string Title,
        string Purpose,
        string[] Entities);

    private static readonly Dictionary<string, OperationalModuleSeed> Catalog = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tributario"] = new OperationalModuleSeed("Governo", "Tributário", "Gerencia contribuintes, imóveis, débitos, dívida ativa e emissão de guias demonstrativas.", new[] { "Contribuintes", "Imoveis", "Economicos", "Debitos", "Guias", "DividaAtiva" }),
        ["Protocolo"] = new OperationalModuleSeed("Governo", "Protocolo", "Controla abertura, tramitação, pendências, anexos e linha do tempo de processos administrativos.", new[] { "Processos", "Novo", "Tramitar", "MinhasPendencias" }),
        ["Ged"] = new OperationalModuleSeed("Governo", "GED/OCR", "Centraliza documentos, pastas, upload visual, OCR, versões e cuidados LGPD.", new[] { "Documentos", "Pastas", "NovoDocumento" }),
        ["Contratos"] = new OperationalModuleSeed("Governo", "Contratos", "Acompanha contratos, vigência, fiscais, fornecedores, aditivos, documentos e vencimentos.", new[] { "Listar", "Novo", "Vencimentos" }),
        ["Juridico"] = new OperationalModuleSeed("Governo", "Jurídico", "Organiza processos, prazos, pareceres, audiências, responsáveis e alertas legais.", new[] { "Processos", "Prazos", "Pareceres", "Audiencias" }),
        ["Rh"] = new OperationalModuleSeed("Governo", "RH", "Integra servidores, vínculos, folha, férias, afastamentos e saúde ocupacional.", new[] { "Servidores", "Folhas", "Ferias", "Afastamentos" }),
        ["Saude"] = new OperationalModuleSeed("Governo", "Saúde", "Acompanha pacientes, unidades, atendimentos, agendas e ACS/campo.", new[] { "Pacientes", "Unidades", "Atendimentos", "Agendas", "Acs" }),
        ["Educacao"] = new OperationalModuleSeed("Governo", "Educação", "Gerencia alunos, escolas, turmas, matrículas e frequência escolar.", new[] { "Alunos", "Escolas", "Turmas", "Matriculas", "Frequencia" }),
        ["Agro"] = new OperationalModuleSeed("Governo", "Agro", "Apoia produtores, propriedades, patrulha mecanizada, feiras e produção rural.", new[] { "Produtores", "Propriedades", "Programas" }),
        ["Saneamento"] = new OperationalModuleSeed("Governo", "Saneamento", "Controla consumidores, ligações, leituras, faturas, ordens de serviço e GIS.", new[] { "Consumidores", "Ligacoes", "Leituras", "Faturas", "OrdensServico", "Gis" }),
        ["Social"] = new OperationalModuleSeed("Governo", "Social", "Gerencia famílias, atendimentos, benefícios, visitas e acompanhamento socioassistencial.", new[] { "Familias", "Atendimentos", "Beneficios" }),
        ["Comercial"] = new OperationalModuleSeed("Empresas", "Comercial/CRM", "Controla clientes, leads, propostas, pedidos e funil comercial.", new[] { "Clientes", "Leads", "Propostas", "Pedidos", "Funil" }),
        ["Varejo"] = new OperationalModuleSeed("Empresas", "Varejo/PDV", "Demonstra vendas, caixa, produtos e fechamento operacional de PDV.", new[] { "Vendas", "Caixa", "Produtos" }),
        ["Atacado"] = new OperationalModuleSeed("Empresas", "Atacado", "Acompanha pedidos, clientes, tabelas comerciais e separação logística.", new[] { "Pedidos", "Clientes", "Separacao" }),
        ["Estoque"] = new OperationalModuleSeed("Empresas", "Estoque", "Controla saldos, movimentações, inventário e alertas de reposição.", new[] { "Produtos", "Movimentacoes", "Inventario" }),
        ["Financeiro"] = new OperationalModuleSeed("Empresas", "Financeiro", "Acompanha contas a receber, pagar, caixa, categorias e relatórios.", new[] { "ContasReceber", "ContasPagar", "Caixa", "Categorias", "Relatorios" }),
        ["Industria"] = new OperationalModuleSeed("Empresas", "Indústria", "Demonstra ordens de produção, apontamentos, qualidade e custos.", new[] { "Ordens", "Apontamentos", "Qualidade" }),
        ["Manutencao"] = new OperationalModuleSeed("Empresas", "Manutenção", "Gerencia ativos, planos preventivos, SLA e ordens técnicas.", new[] { "Ativos", "PlanosPreventivos" }),
        ["OrdemServico"] = new OperationalModuleSeed("Empresas", "Ordem de Serviço", "Controla abertura, técnico, checklist, apontamentos, SLA e conclusão de OS.", new[] { "Listar", "Nova" }),
        ["MobileCampo"] = new OperationalModuleSeed("Operação", "Mobile/Campo", "Apoia equipes externas com roteiros, coletas, evidências e sincronização.", new[] { "Roteiros", "Coletas" }),
        ["Integracoes"] = new OperationalModuleSeed("Operação", "Integrações", "Monitora conectores, webhooks, filas, erros e reprocessamentos.", new[] { "Conectores", "Webhooks" }),
        ["IA"] = new OperationalModuleSeed("Operação", "IA", "Oferece assistentes, automações, triagem e apoio contextual auditável.", new[] { "Assistentes", "Automacoes" })
    };

    public OperationalModuleViewModel Build(string module, string screen = "Dashboard", string? q = null) => BuildFallbackModel(module, screen, q, Array.Empty<string>());

    public async Task<OperationalModuleViewModel> BuildFallbackAsync(string module, string screen = "Dashboard", string? q = null, CancellationToken cancellationToken = default)
    {
        var tables = GetModuleTables(module);
        var existingTables = await InspectExistingTablesAsync(tables, cancellationToken).ConfigureAwait(false);
        var usesRealData = false;
        var status = existingTables.Count > 0 ? "Parcial" : "Em implantação";
        return BuildFallbackModel(module, screen, q, existingTables);
    }

    private static OperationalModuleViewModel BuildFallbackModel(string module, string screen, string? q, IReadOnlyList<string> existingTables)
    {
        var usesRealData = false;
        var status = existingTables.Count > 0 ? "Parcial" : "Em implantação";
        var statusMessage = existingTables.Count > 0
            ? $"Schema operacional detectado: {string.Join(", ", existingTables.Select(t => "sigov." + t))}. Esta visão é fallback honesto até a consulta real da tela ser ativada."
            : "Nenhuma tabela operacional homologada foi localizada para este módulo. A tela permanece em fallback honesto, sem simular salvamento.";
        var item = Catalog.TryGetValue(module, out var found)
            ? found
            : new OperationalModuleSeed("Operação", module, "Fluxo em implantação com navegação demonstrável.", new[] { "Listar", "Novo" });
        var baseUrl = "/" + module;
        var actions = item.Entities.Select(e => new QuickAction(e, e.Equals("Dashboard", StringComparison.OrdinalIgnoreCase) ? baseUrl : $"{baseUrl}/{e}")).Prepend(new QuickAction("Dashboard", baseUrl, "⌂")).ToArray();
        return new OperationalModuleViewModel
        {
            Area = item.Area,
            ModuleKey = module,
            Title = item.Title,
            Purpose = item.Purpose,
            Status = status,
            PageStatus = new OperationalPageStatusViewModel { Modulo = item.Title, Status = status, UsaDadosReais = usesRealData, UsaFallback = !usesRealData, Mensagem = statusMessage },
            SchemaTables = existingTables,
            Description = statusMessage,
            CurrentScreen = screen,
            ManualUrl = $"/Manual?modulo={Uri.EscapeDataString(module)}",
            ShowLgpdWarning = module is "Ged" or "Saude" or "Rh" or "Social" or "Tributario" or "Protocolo" or "Juridico",
            Kpis = BuildKpis(module),
            Actions = actions,
            NextSteps = new[] { "Validar parâmetros do tenant", "Conferir permissões por perfil", "Importar dados reais quando tabelas estiverem homologadas", "Ativar auditoria de ações críticas" },
            Timeline = new[]
            {
                new TimelineStep("Criado", "Fallback visual honesto; nenhuma persistência simulada.", "Concluído", "D-5"),
                new TimelineStep("Em análise", "Setor responsável revisou dados e anexos.", "Em andamento", "D-2"),
                new TimelineStep("Próxima ação", "Aguardando confirmação do operador com ponto de auditoria.", "Pendente", "Hoje")
            },
            Records = BuildRecords(module, q),
            EntitySingular = screen.TrimEnd('s').ToLowerInvariant(),
            EntityPlural = screen.ToLowerInvariant()
        };
    }

    private async Task<IReadOnlyList<string>> InspectExistingTablesAsync(IReadOnlyList<string> tables, CancellationToken cancellationToken)
    {
        var found = new List<string>();
        foreach (var table in tables)
        {
            try
            {
                if (await _schemaInspector.TableExistsAsync("sigov", table, cancellationToken).ConfigureAwait(false))
                {
                    found.Add(table);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao inspecionar tabela operacional sigov.{Table}", table);
            }
        }
        return found;
    }

    private static IReadOnlyList<string> GetModuleTables(string module) => module switch
    {
        "Protocolo" => new[] { "protocolo", "processo", "tramite", "protocolo_movimento", "protocolo_anexo", "arquivo" },
        "Ged" => new[] { "documento", "ged_documento", "ged_pasta", "pasta", "documento_versao", "arquivo", "ocr_fila" },
        "Tributario" => new[] { "contribuinte", "imovel", "debito", "guia", "divida_ativa" },
        "Contratos" => new[] { "contrato", "contrato_aditivo", "contrato_fiscal", "contrato_documento" },
        "Juridico" => new[] { "processo_juridico", "parecer_juridico", "prazo_juridico", "audiencia_juridica" },
        "Financeiro" => new[] { "conta_pagar", "conta_receber", "caixa_movimento", "categoria_financeira" },
        _ => Array.Empty<string>()
    };

    private static IReadOnlyList<ModuleKpi> BuildKpis(string module) => module switch
    {
        "Tributario" => new[] { new ModuleKpi("Arrecadação", "R$ 428 mil", "Competência atual"), new ModuleKpi("Débitos", "312", "Em cobrança"), new ModuleKpi("Contribuintes", "8.420", "CPF/CNPJ mascarado"), new ModuleKpi("Guias", "76", "Emitidas hoje", "success") },
        "Protocolo" => new[] { new ModuleKpi("Pendências", "24", "Minhas filas"), new ModuleKpi("Em andamento", "156", "Processos ativos"), new ModuleKpi("Concluídos", "39", "Últimos 7 dias", "success") },
        "Ged" => new[] { new ModuleKpi("Documentos", "2.340", "Indexados"), new ModuleKpi("OCR pendente", "18", "Fila segura", "warning"), new ModuleKpi("Recentes", "42", "Últimas 24h") },
        _ => new[] { new ModuleKpi("Registros", "0", "Fallback sem persistência"), new ModuleKpi("Pendências", "12", "Aguardando ação", "warning"), new ModuleKpi("Concluídos", "87", "No mês", "success"), new ModuleKpi("Alertas", "3", "Requer atenção", "danger") }
    };

    private static IReadOnlyList<DemoRecord> BuildRecords(string module, string? q)
    {
        var docs = module is "Saude" or "Rh" or "Social" or "Tributario" ? new[] { "***.123.456-**", "***.987.654-**", "12.***.***/0001-**" } : new[] { "SIG-2026-001", "SIG-2026-002", "SIG-2026-003" };
        var rows = new[]
        {
            new DemoRecord(1, $"{module[..Math.Min(3, module.Length)].ToUpperInvariant()}-001", "Registro demonstrativo prioritário", "Em andamento", "Secretaria responsável", "Hoje", docs[0]),
            new DemoRecord(2, $"{module[..Math.Min(3, module.Length)].ToUpperInvariant()}-002", "Registro com validação pendente", "Pendente", "Operador do módulo", "Ontem", docs[1]),
            new DemoRecord(3, $"{module[..Math.Min(3, module.Length)].ToUpperInvariant()}-003", "Registro concluído para histórico", "Concluído", "Auditoria interna", "D-3", docs[2])
        };
        return string.IsNullOrWhiteSpace(q) ? rows : rows.Where(r => (r.Nome + r.Codigo + r.Status).Contains(q, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
}
