# RC50.63 — plano de consolidação transversal

Data do inventário: 2026-08-19. O inventário é estático e não substitui homologação autenticada.

## Inventário

- **Módulos existentes:** catálogo com 50 itens; núcleos priorizados: Tributário, Financeiro, Saneamento, Educação, Saúde, Processos, GED, Assinaturas, Legislativo, Diário, Transparência, Ouvidoria/e-SIC, RH/Folha, Compras/Licitações, Contratos, Almoxarifado, Patrimônio, Frotas e Obras.
- **Com dashboard:** Tributário, Financeiro, Saneamento, Educação, Saúde, Processos, GED, Legislativo, RH, Compras, Contratos, Frotas e Obras têm superfícies localizadas. **Sem dashboard comprovado:** Assinaturas, e-SIC isolado, Almoxarifado e Patrimônio; status permanece não comprovado quando a tabela canônica não existe.
- **Com pendências/alertas:** todos os módulos podem publicar nas centrais; a RC50.63 persiste ocorrências sem duplicar pendência aberta. Módulos sem produtor conectado aparecem com estado vazio seguro, não com dados fictícios.
- **Dados sensíveis:** Saúde, Educação, RH/Folha, Tributário, Ouvidoria/e-SIC, GED/Processos e LGPD. As centrais exibem metadados, jamais documento, contato, conteúdo clínico ou documento sigiloso.
- **Exportação existente:** Financeiro, Tributário, Educação, Saúde, GED, Auditoria/LGPD e relatórios administrativos têm superfícies heterogêneas; exportações transversais permanecem bloqueadas até auditoria específica ser concluída.
- **Integração financeira:** Tributário, Saneamento, Folha e Compras/Contratos. **GED/Processos:** Assinaturas, Ouvidoria/e-SIC e módulos com anexação. Eventos reais são separados de `PREPARATORIA`.
- **Perfis:** SuperAdmin/Administrador Geral, AdminTenant, Secretário/Gestor, Coordenador, Funcionário Financeiro, Professor, ACS, Almoxarifado, Fiscal de Contrato/Obra, Auditor e Atendimento, além dos templates setoriais das RC50.60–62.
- **Permissões:** catálogo granular anterior mais `governanca.{pendencias,alertas,qualidade,integracoes,status_funcional}`; backend continua autoridade.

## Achados e tratamento

- Links já visíveis para `/IntegracoesInternas` e `/QualidadeDados` não tinham superfície transversal comprovada; foram ligados a controllers reais. Foram incluídos Pendências, Alertas e Status Funcional somente para perfis/grants de governança.
- Nenhum padrão estático `501` essencial foi identificado. Ausência de 404/500 exige smoke runtime.
- Minha Central usava recomendações e pendências fixas e consultava auditoria sem `tenant_id`; RC50.63 troca por perfil, tabelas reais/estado vazio e filtro obrigatório de tenant.
- Dashboard executivo legado ainda possui indicadores estruturais heterogêneos; produtores sem estrutura devem declarar pendência, jamais simular integração ou valor.
- Regras pendentes: prova E2E de integrações financeiras, producers automáticos de todas as inconsistências, exportações transversais auditadas, escopo fino unidade/turma/microárea e homologação autenticada.

## P0/P1/P2 inicial

- **P0:** apply/build/smoke e autorização autenticada ainda sem evidência; nenhuma promoção antes dos gates.
- **P1:** conectar cada módulo aos produtores de ocorrência; comprovar rotas de ação e exportação auditada.
- **P2:** cache medido de KPI, filtros salvos e polimento visual.
