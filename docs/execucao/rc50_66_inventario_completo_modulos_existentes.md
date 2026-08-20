# RC50.66 — inventário completo de módulos existentes

Data: 2026-08-19. Inventário consolidado por família para evitar declarar como completo aquilo que ainda está em construção. `Web/API` indica superfícies localizadas, não homologação HTTP.

| Família (código) | Área e superfícies | Persistência/permissão/perfis | Dashboard, menu e integrações | Status, pendência e ação RC50.66 |
|---|---|---|---|---|
| Governança (`governanca`) | Web/API: MinhaCentral, Pendencias, Alertas, QualidadeDados, IntegracoesInternas, Modulos | migration RC50.63; grants `governanca.*`; admin, gestor, coordenador, auditor, leitura | centrais e status funcional; integração transversal | real sem prova runtime; preservado |
| Segurança/LGPD/Auditoria/Observabilidade (`seguranca`,`lgpd`,`auditoria`) | controllers, services, repositories e views próprios | migrations RC50.51/52; SuperAdmin, AdminTenant e Auditor | Catálogo, Matriz e trilhas | estrutural; preservado |
| Tributário/Financeiro (`tributario`,`financeiro`) | Web/API, Dapper e dashboards | migrations do bloco 5 e Tributário avançado; financeiro/grants segregados | pontes arrecadação, compras e saneamento | parcial; não alterado |
| Saneamento (`saneamento`) | Comercial, faturamento, operação e GIS | repositories/migrations; perfis operacionais | dashboard e Financeiro | parcial; não alterado |
| Educação/Saúde (`educacao`,`saude`) | Web/API, campo/ACS, serviços avançados | migrations e grants RC50.60; escopo escola/unidade | dashboards e integrações setoriais | parcial; PII exige homologação |
| Processos/GED/Assinaturas (`processos`,`ged`,`assinaturas`) | protocolo, processos, documentos e validação | migrations/repositories; Atendimento, Gestor, Auditor | menu e integrações documentais | Processos real; demais parciais |
| Legislativo/Diário/Transparência (`legislativo`,`diario_oficial`,`transparencia`) | controllers/views e publicação | migrations do bloco 8; grants de publicar | dashboards/menu; Processos/GED | em construção; preservado |
| Ouvidoria/e-SIC (`ouvidoria`,`esic`) | atendimento, protocolo e respostas | Dapper/migrations; Atendimento e leitura controlada | menu/Processos | parcial; preservado |
| RH/Folha (`rh`,`folha`) | controllers/views/services/repositories | migrations; RH, gestor e financeiro segregados | dashboards; ponte Financeiro preparatória | parcial; preservado |
| Compras/Licitações/Contratos (`compras`,`licitacoes`,`contratos`) | requisição, processo, contrato e medição | migrations do bloco 6; grants de aprovação | dashboards e Financeiro | parcial; preservado |
| Almoxarifado/Patrimônio (`almoxarifado`,`patrimonio`) | estoque, recebimento, bens e inventário | Dapper/migrations; operacional/auditor | menus e integração interna | parcial; preservado |
| Frotas/Obras (`frotas`,`obras`) | veículos, diários, obras e medições | Dapper/migrations; operação/gestão | dashboards e contratos | parcial; preservado |
| Assistência Social (`social`) | famílias, atendimento e benefícios | controllers/repositories/migrations; PII restrita | dashboard/menu | em construção; preservado |
| Empresarial (`comercial`,`ordem_servico`,`estoque_compras`,`industria_producao`) | SaaS, CRM, OS, estoque e indústria | controllers/services/repositories/migrations; perfis do tenant | catálogo/menu e Financeiro empresarial | em construção; preservado |
| Agro (`agro`) | Web `/Agro/*`; APIs `api/agro/*`; views e scripts `agro.*.js`; services e repositories Dapper | migrations `026` e `202606081*`; catálogo granular `agro.*`; Técnico Rural, Operador, Gestor, Coordenador, leitura | dashboard/menu; processos, GED, Financeiro, Transparência e centrais | funcional/parcial; autenticação Web reforçada e catálogo atualizado |
| Geo (subdomínio Agro) | Web Mapa/Camadas; API camadas/feições/export; `agro.geo.js` | `agro_geo_camada/feicao`; grants visualizar/criar/editar/excluir/exportar | links novos no menu Agro; GeoJSON | funcional/parcial; export auditado nesta RC |

## Inventário Agro/Geo detalhado

- **Views:** Dashboard, MapaRural, CamadasGeo, Produtores/Detalhe, Propriedades/Detalhe, Talhoes, Culturas, Safras, Producao, Programas, Beneficios/Concessoes, Insumos/Distribuicao, Patrulha, Maquinas/Implementos/Agenda/Servicos, estradas, feiras, agroindústrias, BI, relatórios e transparência.
- **Scripts:** `agro.crud.js`, `agro.geo.js`, `agro.mapa-rural.js` e scripts por recurso.
- **Serviços/repositories:** namespaces `Sigov.Application.Agro` e `Sigov.Infrastructure.Agro`; SQL parametrizado, projeções explícitas e filtro `tenant_id` nas consultas revisadas.
- **Perfis:** SuperAdmin/AdminTenant, gestores/coordenadores concedidos, Técnico Rural (perfil operacional concedido), Operador Patrulha, Auditor leitura e Atendimento Rural restrito por grants.
- **Pendências reais:** execução autenticada multi-tenant, produtores transversais contínuos e integrações financeira/documental ponta a ponta aguardam ambiente; não há botão/endpoint removido para mascará-las.
