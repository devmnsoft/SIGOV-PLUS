namespace Sigov.Application.Commercial;

public sealed class ModuleCatalogService : IModuleCatalogService
{
    private static readonly IReadOnlyList<ModuleCatalogItem> Modules = BuildModules();
    private static readonly IReadOnlyList<ModuleCatalogPackage> Packages = BuildPackages();

    public IReadOnlyList<ModuleCatalogItem> GetModules() => Modules;

    public IReadOnlyList<ModuleCatalogPackage> GetSuggestedPackages() => Packages;

    public ModuleCatalogItem? FindByCode(string code) => Modules.FirstOrDefault(module => string.Equals(module.Code, code, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<ModuleCatalogItem> BuildModules()
    {
        var modules = new[]
        {
            Create("core", "Core e Cadastros", "Fundação", "Base única de pessoas, entidades e parâmetros municipais.", ModuleStatus.Habilitado, "bi-diagram-3", "/Pessoas", "core.pessoas.visualizar"),
            Create("seguranca", "Segurança", "Governança", "Perfis, permissões e proteção operacional por tenant.", ModuleStatus.Contratado, "bi-shield-lock", "/SaasAdmin/Operacao", "seguranca.usuario.visualizar"),
            Create("auditoria-lgpd", "Auditoria e LGPD", "Governança", "Rastreabilidade, classificação e mascaramento de dados pessoais.", ModuleStatus.Contratado, "bi-clipboard-check", "/Integracoes/Logs", "auditoria.visualizar"),
            Create("processos", "Processos Digitais", "Atendimento", "Protocolos, processos, pareceres e diário oficial integrados.", ModuleStatus.Habilitado, "bi-folder-check", "/Processos", "processos.processo.visualizar"),
            Create("financeiro", "Financeiro/SIAFIC", "Gestão fiscal", "Execução orçamentária, empenhos, liquidações, pagamentos e receitas.", ModuleStatus.EmImplantacao, "bi-cash-coin", "/Financeiro/Dashboard", "financeiro.dashboard.visualizar"),
            Create("tributario", "Tributário", "Receita municipal", "Lançamentos, arrecadação, certidões e dívida ativa estrutural.", ModuleStatus.Disponivel, "bi-receipt", "/Comercial", "tributario.visualizar"),
            Create("compras", "Compras e Contratos", "Suprimentos", "Solicitações, contratos, medições e governança de aquisições.", ModuleStatus.Disponivel, "bi-cart-check", "/Comercial", "compras.visualizar"),
            Create("almoxarifado", "Almoxarifado", "Suprimentos", "Entrada, saída e saldo de materiais por unidade administrativa.", ModuleStatus.Disponivel, "bi-box-seam", "/Comercial", "almoxarifado.visualizar"),
            Create("patrimonio", "Patrimônio", "Suprimentos", "Bens, movimentações, baixa e inventário patrimonial.", ModuleStatus.Disponivel, "bi-building", "/Comercial", "patrimonio.visualizar"),
            Create("frotas", "Frotas", "Operação", "Veículos, abastecimentos, manutenção e disponibilidade.", ModuleStatus.Disponivel, "bi-truck", "/Comercial", "frotas.visualizar"),
            Create("obras", "Obras", "Operação", "Acompanhamento físico-financeiro e evidências de obras públicas.", ModuleStatus.Disponivel, "bi-cone-striped", "/Comercial", "obras.visualizar"),
            Create("rh", "RH/Folha/Ponto", "Pessoas", "Servidores, vínculos, folha, ponto e portal do servidor.", ModuleStatus.EmImplantacao, "bi-people", "/Rh/Dashboard", "rh.dashboard.visualizar"),
            Create("educacao", "Educação", "Políticas públicas", "Escolas, turmas, matrículas, frequência e indicadores educacionais.", ModuleStatus.EmImplantacao, "bi-mortarboard", "/Educacao/Dashboard", "educacao.dashboard.visualizar"),
            Create("saude", "Saúde/ACS", "Políticas públicas", "Unidades, pacientes, atendimentos, farmácia, ACS e dados sensíveis protegidos.", ModuleStatus.Beta, "bi-heart-pulse", "/Saude/Dashboard", "saude.dashboard.visualizar"),
            Create("saneamento", "Saneamento", "Serviços urbanos", "Consumidores, leituras, faturas, ordens e operação de campo.", ModuleStatus.Beta, "bi-droplet", "/Saneamento/Dashboard", "saneamento.dashboard.visualizar"),
            Create("social", "Assistência Social", "Políticas públicas", "Famílias, atendimentos, benefícios, pareceres e vigilância socioassistencial.", ModuleStatus.Beta, "bi-house-heart", "/Social/Dashboard", "social.dashboard.visualizar"),
            Create("agro", "Agro e Desenvolvimento Rural", "Políticas públicas", "Georreferenciamento rural, camadas estruturais e dashboard inicial do Agro.", ModuleStatus.Beta, "bi-tree", "/Agro/Dashboard", "agro.dashboard.visualizar"),
            Create("bi", "Relatórios/BI", "Inteligência", "Indicadores, painéis, exportações e transparência com governança de dados.", ModuleStatus.Disponivel, "bi-bar-chart", "/Executivo", "relatorios.visualizar"),
            Create("transparencia", "Transparência", "Cidadão", "Publicação de informações e dados abertos sem vazamento de dados pessoais.", ModuleStatus.Disponivel, "bi-globe2", "/Comercial", "transparencia.visualizar"),
            Create("integracoes", "Integrações", "Plataforma", "APIs, webhooks, outbox, remessas oficiais e assinatura digital.", ModuleStatus.Habilitado, "bi-plug", "/Integracoes/Dashboard", "integracao.dashboard.visualizar"),
            Create("suporte", "Suporte", "Operação SaaS", "Central de ajuda, chamados, SLAs e acompanhamento operacional.", ModuleStatus.Contratado, "bi-life-preserver", "/Ajuda", "suporte.visualizar"),
            Create("operacao-saas", "Operação SaaS", "Operação SaaS", "Status de tenant, licença, saúde da plataforma e preparação para go-live.", ModuleStatus.Contratado, "bi-cloud-check", "/SaasAdmin/Operacao", "saas.operacao.visualizar"),
            Create("comercial", "Comercial/CRM", "Clientes privados", "Clientes, leads, oportunidades, propostas, pedidos e tabela de preços com LGPD.", ModuleStatus.Disponivel, "bi-briefcase", "/Comercial/Dashboard", "comercial.visualizar"),
            Create("ordem_servico", "Ordem de Serviço", "Serviços", "OS técnica, agenda, checklist, apontamentos, peças e anexos integrados.", ModuleStatus.Disponivel, "bi-tools", "/OrdemServico/Dashboard", "os.visualizar"),
            Create("manutencao_industrial", "Manutenção Industrial", "Indústria", "Ativos, planos preventivos, medidores, paradas e causas de falha.", ModuleStatus.Disponivel, "bi-gear-wide-connected", "/Industrial/Dashboard", "industrial.visualizar"),
            Create("estoque_compras", "Estoque e Compras", "Suprimentos privados", "Produtos, almoxarifados, saldos, requisições, fornecedores e pedidos de compra.", ModuleStatus.Disponivel, "bi-boxes", "/Estoque/Dashboard", "estoque.visualizar"),
            Create("comercio_varejo", "Comércio Varejista", "Comércio", "Base navegável para varejo sem PDV fiscal completo nesta etapa.", ModuleStatus.Disponivel, "bi-shop", "/Comercio/Varejo", "comercio.varejo.visualizar"),
            Create("comercio_atacado", "Comércio Atacadista", "Comércio", "Base de pedidos, produtos e operação B2B atacadista sem fiscal nesta etapa.", ModuleStatus.Disponivel, "bi-buildings", "/Comercio/Atacado", "comercio.atacado.visualizar"),
            Create("industria_producao", "Indústria Produção", "Indústria", "Base de produção futura integrada a ativos, estoque e manutenção sem MRP completo.", ModuleStatus.Disponivel, "bi-cpu", "/Industrial/Dashboard", "industrial.producao.visualizar"),
            Create("financeiro_empresarial", "Financeiro Empresarial", "Gestão empresarial", "Eventos financeiros futuros gerados por vendas, OS e compras sem financeiro completo.", ModuleStatus.Disponivel, "bi-bank", "/Financeiro/Dashboard", "financeiro.empresarial.visualizar")
        };

        return modules;
    }


    private static IReadOnlyList<ModuleCatalogPackage> BuildPackages()
    {
        return new[]
        {
            new ModuleCatalogPackage("BUSINESS_STARTER", "Business Starter", new[] { "comercial", "ordem_servico", "estoque_compras" }),
            new ModuleCatalogPackage("COMERCIO_PLUS", "Comércio Plus", new[] { "comercial", "comercio_varejo", "comercio_atacado", "estoque_compras", "financeiro_empresarial" }),
            new ModuleCatalogPackage("INDUSTRIAL_PLUS", "Industrial Plus", new[] { "comercial", "ordem_servico", "manutencao_industrial", "industria_producao", "estoque_compras", "financeiro_empresarial" }),
            new ModuleCatalogPackage("SERVICE_DESK_PRO", "Service Desk Pro", new[] { "comercial", "ordem_servico", "contratos", "ged", "financeiro_empresarial" })
        };
    }

    private static ModuleCatalogItem Create(string code, string name, string category, string description, ModuleStatus status, string icon, string route, string permission)
    {
        return new ModuleCatalogItem(
            code,
            name,
            category,
            description,
            status,
            icon,
            route,
            new[] { permission },
            new[]
            {
                new ModuleFeatureItem("Fluxo operacional", "Tela principal, filtros, ações seguras e auditoria por tenant."),
                new ModuleFeatureItem("Indicadores", "KPIs estruturais para apresentação executiva e acompanhamento diário."),
                new ModuleFeatureItem("Governança", "Permissões, LGPD e isolamento SaaS aplicados desde a fundação.")
            },
            new[]
            {
                new ModuleBenefitItem("Visão integrada", "Reduz retrabalho ao usar cadastros e regras compartilhadas."),
                new ModuleBenefitItem("Implantação guiada", "Permite apresentar valor rapidamente em homologação."),
                new ModuleBenefitItem("Operação segura", "Evita vazamento entre tenants e padroniza auditoria.")
            },
            new[]
            {
                new ModuleKpiItem("Status", status.ToString(), "Situação comercial ou operacional no tenant"),
                new ModuleKpiItem("Funcionalidades", "3+", "Blocos disponíveis no catálogo"),
                new ModuleKpiItem("Permissões", "1+", "Permissões mínimas para acesso")
            },
            status is ModuleStatus.Beta or ModuleStatus.EmImplantacao);
    }
}
