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
            Create("tributario", "Tributário Avançado", "Receita municipal", "IPTU, ISS, taxas, dívida ativa, parcelamentos, DAM simulado, NFS-e simulada e livro eletrônico fiscal.", ModuleStatus.Disponivel, "bi-receipt", "/Tributario/Dashboard", "tributario.dashboard.visualizar"),
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
            Create("comercio_varejo", "Comércio Varejista", "Comércio", "Varejo avançado com vendas balcão, PDV web inicial, caixa e estoque integrado.", ModuleStatus.Disponivel, "bi-shop", "/Varejo/Dashboard", "comercio.pdv.acessar"),
            Create("pdv", "PDV Web", "Comércio", "Ponto de venda web inicial com carrinho, pagamentos e fechamento não fiscal.", ModuleStatus.Disponivel, "bi-upc-scan", "/Comercio/PDV", "comercio.pdv.acessar"),
            Create("caixa", "Caixa Comercial", "Comércio", "Abertura, suprimento, sangria, fechamento e resumo por forma de pagamento.", ModuleStatus.Disponivel, "bi-cash-stack", "/Comercio/Caixa", "comercio.caixa.abrir"),
            Create("comercio_atacado", "Comércio Atacadista", "Comércio", "Pedidos B2B, tabelas de preço, separação, conferência e faturamento inicial.", ModuleStatus.Disponivel, "bi-buildings", "/Atacado/Dashboard", "comercio.pedidos.visualizar"),
            Create("industria_producao", "Indústria e Produção", "Indústria", "Produção por ordem, BOM, roteiro, chão de fábrica, qualidade e custos sem MRP completo.", ModuleStatus.Disponivel, "bi-cpu", "/Industria/Dashboard", "industria.dashboard.visualizar"),
            Create("financeiro_empresarial", "Financeiro Empresarial", "Gestão empresarial", "Plano de contas, centros de custo, contas, baixas, caixa, fluxo, conciliação e integração comercial/industrial.", ModuleStatus.Disponivel, "bi-bank", "/Financeiro/Dashboard", "financeiro.dashboard.visualizar"),
            Create("ged", "GED/OCR e Automação Documental", "Documentos", "GED completo com upload/download, OCR simulado, metadados, workflow, tramitação e histórico auditado.", ModuleStatus.Disponivel, "bi-file-earmark-text", "/Ged/Dashboard", "ged.visualizar"),
            Create("ocr", "OCR Documental", "Documentos", "Digitalização OCR simulada com extração de texto, indexação e auditoria por tenant.", ModuleStatus.Disponivel, "bi-filetype-txt", "/Ged/Ocr", "ocr.processar"),
            Create("contrato", "Contratos e Assinaturas", "Jurídico", "Contratos vinculados ao Comercial, Financeiro, Produção e Tributário com assinatura digital simulada.", ModuleStatus.Disponivel, "bi-file-earmark-check", "/Ged/Contratos", "contrato.visualizar"),
            Create("fluxo", "Workflow e Tramitação", "Automação", "Fluxos visuais, protocolos eletrônicos e tramitação de documentos com SLA.", ModuleStatus.Disponivel, "bi-diagram-2", "/Ged/Workflow", "fluxo.visualizar"),
            Create("ia_assistente", "IA Assistente", "Inteligência", "Assistentes operacionais por módulo com provider interno, histórico e auditoria por tenant.", ModuleStatus.Disponivel, "bi-robot", "/IA/Assistente", "ia.assistente.acessar"),
            Create("ia_documental", "IA Documental", "Inteligência", "Resumo, classificação e extração estruturada de documentos com LGPD e revisão humana.", ModuleStatus.Disponivel, "bi-file-earmark-text", "/IA/Documental", "ia.documental.resumir"),
            Create("ia_relatorios", "IA Relatórios", "Inteligência", "Geração assistida de relatórios textuais operacionais, financeiros e fiscais.", ModuleStatus.Disponivel, "bi-bar-chart-line", "/IA/Relatorios", "ia.relatorios.gerar"),
            Create("ia_automacoes", "IA Automações", "Inteligência", "Sugestões, alertas e workflows inteligentes com confirmação humana para ações críticas.", ModuleStatus.Disponivel, "bi-diagram-3", "/IA/Automacoes", "ia.automacoes.visualizar"),
            Create("ia_predicoes", "IA Predições", "Inteligência", "Predições iniciais por regras para inadimplência, ruptura, OS, contratos e produção.", ModuleStatus.Beta, "bi-graph-up-arrow", "/IA/Predicoes", "ia.predicoes.visualizar"),
            Create("financeiro_publico", "Financeiro Público", "Gestão pública", "Módulo futuro para evolução SIAFIC, execução pública e integrações governamentais.", ModuleStatus.EmImplantacao, "bi-building-lock", "/Financeiro/Dashboard", "financeiro_publico.visualizar"),
            Create("mobile_pwa", "Mobile PWA", "Campo e mobilidade", "PWA responsivo, instalável e mobile-first com offline page, cache estático, indicadores de conexão e sincronização.", ModuleStatus.Beta, "bi-phone", "/Mobile/Home", "mobile.acessar"),
            Create("campo_operacional", "Campo Operacional", "Campo e mobilidade", "Agenda, atividades, visitas, checklists, evidências e operação de equipes externas com auditoria por tenant.", ModuleStatus.Beta, "bi-geo-alt", "/Campo/Dashboard", "campo.dashboard.visualizar"),
            Create("georreferenciamento", "Georreferenciamento", "Campo e mobilidade", "Coleta opcional de coordenadas com consentimento/regra operacional, rotas, mapa e trilhas de localização.", ModuleStatus.Beta, "bi-map", "/Mobile/Mapa", "campo.localizacao.enviar"),
            Create("offline_sync", "Offline Sync", "Campo e mobilidade", "Sincronização offline-first por dispositivo, lote, item, tenant, usuário, status e correlationId.", ModuleStatus.Beta, "bi-cloud-arrow-up", "/Mobile/Sync", "mobile.sincronizar"),
            Create("assinatura_campo", "Assinatura em Campo", "Campo e mobilidade", "Coleta de assinatura com hash, evidência, geolocalização opcional e proteção LGPD.", ModuleStatus.Beta, "bi-pen", "/Mobile/Assinatura", "campo.assinatura.coletar"),
            Create("notificacoes_mobile", "Notificações Mobile", "Campo e mobilidade", "Notificações internas simuladas para equipes mobile sem dependência de push externo real.", ModuleStatus.Beta, "bi-bell", "/Mobile/Notificacoes", "campo.notificacoes.visualizar")
        };

        return modules;
    }


    private static IReadOnlyList<ModuleCatalogPackage> BuildPackages()
    {
        return new[]
        {

            new ModuleCatalogPackage("BUSINESS_FINANCE", "Business Finance", new[] { "financeiro_empresarial", "comercial", "estoque_compras" }),
            new ModuleCatalogPackage("COMERCIO_STARTER", "Comércio Starter", new[] { "comercial", "comercio_varejo", "pdv", "caixa", "estoque_compras" }),
            new ModuleCatalogPackage("COMERCIO_PLUS", "Comércio Plus", new[] { "financeiro_empresarial", "comercial", "comercio_varejo", "comercio_atacado", "pdv", "caixa", "estoque_compras" }),
            new ModuleCatalogPackage("ATACADO_PRO", "Atacado Pro", new[] { "comercial", "comercio_atacado", "pedidos", "estoque_compras", "financeiro_empresarial" }),
            new ModuleCatalogPackage("BUSINESS_FULL", "Business Full", new[] { "comercial", "comercio_varejo", "comercio_atacado", "pdv", "caixa", "estoque_compras", "ordem_servico", "manutencao_industrial", "industria_producao", "financeiro_empresarial" }),
            new ModuleCatalogPackage("BUSINESS_STARTER", "Business Starter", new[] { "comercial", "ordem_servico", "estoque_compras" }),
            new ModuleCatalogPackage("INDUSTRIAL_STARTER", "Industrial Starter", new[] { "industria_producao", "estoque_compras", "ordem_servico" }),
            new ModuleCatalogPackage("INDUSTRIAL_PLUS", "Industrial Plus", new[] { "financeiro_empresarial", "industria_producao", "manutencao_industrial", "estoque_compras", "compras", "ordem_servico" }),
            new ModuleCatalogPackage("FACTORY_FULL", "Factory Full", new[] { "industria_producao", "manutencao_industrial", "ordem_servico", "estoque_compras", "comercial", "comercio_atacado", "financeiro_empresarial" }),
            new ModuleCatalogPackage("SERVICE_DESK_PRO", "Service Desk Pro", new[] { "comercial", "ordem_servico", "contrato", "ged", "ocr", "fluxo", "financeiro_empresarial" }),
            new ModuleCatalogPackage("GED_AUTOMACAO_PLUS", "GED Automação Plus", new[] { "ged", "ocr", "contrato", "fluxo", "processos", "integracoes", "auditoria-lgpd" }),
            new ModuleCatalogPackage("AI_STARTER", "AI Starter", new[] { "ia_assistente", "ia_relatorios" }),
            new ModuleCatalogPackage("AI_DOCUMENTAL", "AI Documental", new[] { "ia_assistente", "ia_documental", "ged", "ocr" }),
            new ModuleCatalogPackage("AI_ENTERPRISE", "AI Enterprise", new[] { "ia_assistente", "ia_documental", "ia_relatorios", "ia_automacoes", "ia_predicoes", "integracoes" }),
            new ModuleCatalogPackage("BUSINESS_FULL_AI", "Business Full AI", new[] { "comercial", "financeiro_empresarial", "estoque_compras", "ordem_servico", "ia_assistente", "ia_relatorios", "ia_automacoes" }),
            new ModuleCatalogPackage("GOV_FULL_AI", "Gov Full AI", new[] { "tributario", "protocolo", "ged", "contratos", "financeiro_publico", "ia_assistente", "ia_documental", "ia_relatorios", "ia_automacoes" }),
            new ModuleCatalogPackage("GOV_PLUS", "Gov Plus", new[] { "financeiro_publico", "tributario", "contrato", "compras", "rh", "processos", "ged", "ocr", "fluxo" }),
            new ModuleCatalogPackage("CAMPO_STARTER", "Campo Starter", new[] { "mobile_pwa", "campo_operacional", "offline_sync" }),
            new ModuleCatalogPackage("FIELD_SERVICE_PRO", "Field Service Pro", new[] { "mobile_pwa", "campo_operacional", "ordem_servico", "georreferenciamento", "assinatura_campo", "notificacoes_mobile" }),
            new ModuleCatalogPackage("SAUDE_CAMPO", "Saúde Campo", new[] { "mobile_pwa", "saude", "campo_operacional", "georreferenciamento", "offline_sync" }),
            new ModuleCatalogPackage("SANEAMENTO_CAMPO", "Saneamento Campo", new[] { "mobile_pwa", "saneamento", "ordem_servico", "georreferenciamento", "offline_sync" }),
            new ModuleCatalogPackage("AGRO_CAMPO", "Agro Campo", new[] { "mobile_pwa", "agro", "campo_operacional", "georreferenciamento", "offline_sync" }),
            new ModuleCatalogPackage("GOV_CAMPO_FULL", "Gov Campo Full", new[] { "mobile_pwa", "protocolo", "tributario", "saude", "saneamento", "agro", "social", "georreferenciamento", "offline_sync", "assinatura_campo" })
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
