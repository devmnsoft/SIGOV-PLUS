namespace Sigov.Application.Saas.Modules;

public sealed class ModuleCatalogService : IModuleCatalogService
{
    private static readonly IReadOnlyList<ModuleCatalogItem> Modules = BuildModules();
    private static readonly IReadOnlyList<ModulePackageItem> Packages = BuildPackages();

    public IReadOnlyCollection<ModuleCatalogItem> GetModules() => Modules;

    public ModuleCatalogItem? FindByCode(string codigo) => Modules.FirstOrDefault(module => string.Equals(module.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyCollection<ModulePackageItem> GetPackages() => Packages;

    public ModulePackageItem? FindPackageByCode(string codigo) => Packages.FirstOrDefault(package => string.Equals(package.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ModuleCatalogItem> BuildModules()
    {
        return new[]
        {
            Module("core", "Core", "Cadastros estruturantes, entidades, exercícios e base operacional.", "Fundação", false, true, Array.Empty<string>()),
            Module("seguranca", "Segurança", "Usuários, grupos, perfis, permissões e autenticação.", "Fundação", false, true, new[] { "core" }),
            Module("auditoria", "Auditoria", "Trilhas, conformidade e rastreabilidade operacional.", "Governança", false, true, new[] { "core" }),
            Module("lgpd", "LGPD", "Privacidade, mascaramento e controle de dados pessoais.", "Governança", false, true, new[] { "core", "auditoria" }),
            Module("processos", "Processos Digitais", "Processos, protocolos, movimentações e documentos.", "Administrativo", true, true, new[] { "core", "seguranca" }),
            Module("financeiro", "Financeiro", "Orçamento, empenhos, liquidações, pagamentos e receitas.", "Financeiro", true, true, new[] { "core" }),
            Module("tributario", "Tributário", "Base parametrizável para arrecadação e cadastros tributários.", "Receita", true, true, new[] { "core" }),
            Module("compras", "Compras", "Planejamento de compras, processos e cotações.", "Administrativo", true, true, new[] { "core" }),
            Module("contratos", "Contratos", "Contratos, vigências, aditivos e fiscalização.", "Administrativo", true, true, new[] { "core", "compras" }),
            Module("almoxarifado", "Almoxarifado", "Materiais, entradas, saídas e estoque.", "Administrativo", true, true, new[] { "core" }),
            Module("patrimonio", "Patrimônio", "Bens, tombamento, movimentações e inventário.", "Administrativo", true, true, new[] { "core" }),
            Module("frotas", "Frotas", "Veículos, motoristas, abastecimentos e manutenção.", "Operacional", true, true, new[] { "core" }),
            Module("obras", "Obras", "Obras, medições, contratos e acompanhamento físico-financeiro.", "Operacional", true, true, new[] { "core" }),
            Module("rh", "Recursos Humanos", "Servidores, cargos, lotações, folha e portal.", "Gestão de Pessoas", true, true, new[] { "core" }),
            Module("educacao", "Educação", "Escolas, matrículas, turmas e acompanhamento educacional.", "Políticas Públicas", true, true, new[] { "core" }),
            Module("saude", "Saúde", "Pacientes, agendas, ACS e serviços de saúde.", "Políticas Públicas", true, true, new[] { "core", "lgpd" }),
            Module("saneamento", "Saneamento", "Unidades consumidoras, medições, qualidade e operações.", "Políticas Públicas", true, true, new[] { "core" }),
            Module("social", "Assistência Social", "Famílias, atendimentos, visitas e benefícios sociais.", "Políticas Públicas", true, true, new[] { "core", "lgpd" }),
            Module("relatorios", "Relatórios", "Relatórios operacionais, gerenciais e consolidados.", "Inteligência", true, true, new[] { "core" }),
            Module("transparencia", "Transparência", "Publicação e consulta pública de dados municipais.", "Governança", true, true, new[] { "core" }),
            Module("integracoes", "Integrações", "APIs, webhooks, outbox e conectores externos.", "Plataforma", true, true, new[] { "core", "auditoria" }),
            Module("suporte", "Suporte", "Chamados, acompanhamento técnico e suporte auditado.", "Plataforma", false, true, new[] { "core" }),
            Module("operacao", "Operação", "Painéis e rotinas operacionais transversais.", "Plataforma", true, true, new[] { "core" }),
            Module("agro", "Agro e Desenvolvimento Rural", "Catálogo contratável futuro para gestão rural e georreferenciamento.", "Políticas Públicas", true, true, new[] { "core", "tributario" }),
            Module("comercial", "Comercial/CRM", "Clientes, leads, oportunidades, propostas, pedidos e preços.", "Empresarial", true, true, new[] { "core", "lgpd" }),
            Module("ordem_servico", "Ordem de Serviço", "OS técnica, agenda, checklist, apontamentos e peças.", "Serviços", true, true, new[] { "core", "comercial", "estoque_compras" }),
            Module("manutencao_industrial", "Manutenção Industrial", "Ativos, planos, medidores, paradas e falhas.", "Indústria", true, true, new[] { "core", "ordem_servico" }),
            Module("estoque_compras", "Estoque e Compras", "Produtos, almoxarifados, saldos, requisições e fornecedores.", "Suprimentos", true, true, new[] { "core" }),
            Module("comercio_varejo", "Comércio Varejista", "Varejo avançado com vendas balcão, PDV web inicial, caixa e estoque integrado.", "Comércio", true, true, new[] { "comercial", "pdv", "caixa", "estoque_compras" }),
            Module("pdv", "PDV Web", "Ponto de venda web inicial com carrinho, pagamentos e fechamento não fiscal.", "Comércio", true, true, new[] { "comercial", "comercio_varejo", "caixa" }),
            Module("caixa", "Caixa Comercial", "Abertura, suprimento, sangria, fechamento e resumo por forma de pagamento.", "Comércio", true, true, new[] { "pdv" }),
            Module("comercio_atacado", "Comércio Atacadista", "Base comercial B2B atacadista integrada a pedidos e estoque.", "Comércio", true, true, new[] { "comercial", "estoque_compras" }),
            Module("industria_producao", "Indústria e Produção", "Produção por ordem, BOM, roteiro, chão de fábrica, qualidade e custos integrados.", "Indústria", true, true, new[] { "core", "estoque_compras" }),
            Module("financeiro_empresarial", "Financeiro Empresarial", "Eventos financeiros futuros para vendas, OS e compras.", "Empresarial", true, true, new[] { "comercial" })
        };
    }

    private static ModuleCatalogItem Module(string codigo, string nome, string descricao, string categoria, bool vendidoSeparadamente, bool podeIntegrar, IReadOnlyCollection<string> dependencias)
    {
        var visualizar = $"{codigo}.visualizar";
        var gerenciar = $"{codigo}.gerenciar";
        return new ModuleCatalogItem(
            codigo,
            nome,
            descricao,
            categoria,
            vendidoSeparadamente,
            podeIntegrar,
            dependencias,
            new[]
            {
                new ModuleFeatureItem($"{codigo}.dashboard", "Dashboard", $"Painel do módulo {nome}."),
                new ModuleFeatureItem($"{codigo}.operacao", "Operação", $"Rotinas operacionais do módulo {nome}.")
            },
            new[] { "Venda separada ou integrada", "Tenant isolation obrigatório", "Parametrização por tenant e exercício" },
            $"/{ToPascal(codigo)}",
            new[] { visualizar, gerenciar });
    }

    private static IReadOnlyList<ModulePackageItem> BuildPackages()
    {
        var todos = Modules.Select(module => module.Codigo).ToArray();
        return new[]
        {
            new ModulePackageItem("ESSENCIAL", "Essencial", "Fundação mínima da plataforma.", new[] { "core", "seguranca", "auditoria", "lgpd", "suporte" }),
            new ModulePackageItem("FINANCEIRO_TRIBUTARIO", "Financeiro e Tributário", "Gestão fiscal, arrecadação e relatórios.", new[] { "financeiro", "tributario", "relatorios" }),
            new ModulePackageItem("GESTAO_ADMINISTRATIVA", "Gestão Administrativa", "Backoffice administrativo municipal.", new[] { "processos", "compras", "contratos", "almoxarifado", "patrimonio", "frotas", "obras" }),
            new ModulePackageItem("SOCIAL_SAUDE_EDUCACAO", "Social, Saúde e Educação", "Políticas públicas integradas.", new[] { "educacao", "saude", "social" }),
            new ModulePackageItem("AGRO_RURAL", "Agro Rural", "Base futura rural integrada com obras, frotas e tributário.", new[] { "agro", "frotas", "obras", "tributario", "relatorios" }),
            new ModulePackageItem("COMERCIO_STARTER", "Comércio Starter", "Varejo com PDV, caixa e estoque em pacote inicial.", new[] { "comercial", "comercio_varejo", "pdv", "caixa", "estoque_compras" }),
            new ModulePackageItem("COMERCIO_PLUS", "Comércio Plus", "Varejo e atacado integrados ao estoque e contas a receber inicial.", new[] { "comercial", "comercio_varejo", "comercio_atacado", "pdv", "caixa", "estoque_compras", "financeiro_empresarial" }),
            new ModulePackageItem("ATACADO_PRO", "Atacado Pro", "Pedidos, separação e financeiro inicial para atacado.", new[] { "comercial", "comercio_atacado", "pedidos", "estoque_compras", "financeiro_empresarial" }),
            new ModulePackageItem("BUSINESS_FULL", "Business Full", "Pacote empresarial integrado com varejo, atacado, OS, manutenção, indústria e financeiro inicial.", new[] { "comercial", "comercio_varejo", "comercio_atacado", "pdv", "caixa", "estoque_compras", "ordem_servico", "manutencao_industrial", "industria_producao", "financeiro_empresarial" }),
            new ModulePackageItem("BUSINESS_STARTER", "Business Starter", "CRM, OS e estoque para operação inicial privada.", new[] { "comercial", "ordem_servico", "estoque_compras" }),
            new ModulePackageItem("INDUSTRIAL_STARTER", "Industrial Starter", "Produção por ordem com estoque e OS.", new[] { "industria_producao", "estoque_compras", "ordem_servico" }),
            new ModulePackageItem("INDUSTRIAL_PLUS", "Industrial Plus", "Produção integrada à manutenção, OS, compras, estoque e financeiro.", new[] { "industria_producao", "manutencao_industrial", "ordem_servico", "estoque_compras", "compras", "financeiro_empresarial" }),
            new ModulePackageItem("FACTORY_FULL", "Factory Full", "Operação fabril completa com comercial atacadista, produção, estoque e financeiro.", new[] { "industria_producao", "manutencao_industrial", "ordem_servico", "estoque_compras", "comercial", "comercio_atacado", "financeiro_empresarial" }),
            new ModulePackageItem("SERVICE_DESK_PRO", "Service Desk Pro", "Serviços com comercial, OS, contratos, GED e financeiro futuro.", new[] { "comercial", "ordem_servico", "contratos", "ged", "financeiro_empresarial" }),
            new ModulePackageItem("COMPLETO", "Completo", "Todos os módulos integrados do sigov.", todos)
        };
    }

    private static string ToPascal(string value) => string.Concat(value.Split('-', '_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
