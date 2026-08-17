# Roadmap próxima sprint

## Gate RC50.41 e próximo Bloco 8

O próximo bloco passa a ser **Bloco 8 — Processos Digitais, Legislativo, GED, Assinaturas, Transparência e Atendimento**. Seu escopo consolidado, requisitos transversais, limites e critérios de aceite estão em [`bloco-8-processos-digitais-legislativo-transparencia.md`](bloco-8-processos-digitais-legislativo-transparencia.md).

Não iniciar sua implementação antes de concluir o RC50.41 com migrations aplicadas no PostgreSQL, build de runtime aprovado, Swagger acessível, login de `admin` e `superadmin` funcional e rotas principais do Bloco 7 validadas. Até a aprovação desse gate, manter o Bloco 8 exclusivamente em planejamento e modelagem conceitual.

1. Executar schema report em PostgreSQL homologado e anexar resultado.
2. Criar migrations idempotentes não destrutivas para tabelas administrativas ausentes.
3. Ativar CRUD real por módulo somente após regras oficiais.
4. Implementar outbox persistente e consumidores de integração.
5. Ampliar permissões por ação: visualizar, criar, editar, excluir, cancelar, estornar, aprovar, homologar, assinar e exportar.
6. Smoke tests autenticados em Docker com screenshots das telas premium.

## Próxima sprint — módulos setoriais

- Criar migrations não destrutivas para tabelas setoriais detectadas como ausentes.
- Implementar repositórios Dapper por entidade após validação de colunas obrigatórias.
- Conectar eventos setoriais ao Workflow/Tarefas/Notificações/Agenda com regras parametrizadas.
- Evoluir CSV seguro e auditoria de exportação por módulo.
- Implementar offline sync real para Mobile/Campo sem simulação.

## Próxima sprint — recomendações pós Patrimônio/Inventário/Obras

- Transparência e Portal do Cidadão com dados públicos derivados de contratos, obras e pagamentos.
- Ouvidoria integrada a protocolo, fiscalização e diário de obra.
- Assinatura Digital para medições, termos de aceite, notificações e relatórios fotográficos.
- API pública com controle de escopo, LGPD e auditoria.
- BI avançado para patrimônio, inventário, obras, medições e SIAFIC.
- e-Sfinge/SIAFIC com validações oficiais e trilha de integração.

## Próxima sprint - Editais, matriz e POC

- Criar migrations não destrutivas para `sigov.edital`, requisitos, evidências, POC e relatório técnico.
- Popular `modulo_saas` com rotas, limitações, documentação e evidências disponíveis.
- Integrar Dashboard, Minha Central, Busca, Relatórios e permissões granulares.
- Implementar storage seguro para anexos com classificação LGPD.
- Implementar exportação PDF/DOCX real para relatório e proposta técnica.
- Adicionar testes automatizados de rotas e regras contra falso atendimento.

## Consolidação funcional SIGOV PLUS - próxima sprint recomendada

### Funcional agora
- Login, Dashboard, relatórios administrativos e módulos com serviços já ligados a tabelas existentes.
- Controllers navegáveis com fallback honesto para áreas ainda não persistidas.

### Parcial com schema
- Protocolo/GED/Workflow/Tarefas/Notificações após aplicação da migration `20260706120000_consolidacao_modulos_transversais.sql`.
- Compras/Licitações/Contratos, Patrimônio/Obras e Portal/Ouvidoria dependem de validação do schema report local.

### Fallback honesto
- Ações oficiais como contrato, empenho, pagamento, tombamento, medição, assinatura e protocolo permanecem sem simulação quando o schema ou integrações oficiais não estiverem homologados.

### Próximas migrations
- Chaves estrangeiras condicionais após auditoria de dados legados.
- Índices específicos por relatórios mais usados.
- Campos de auditoria complementares por usuário/tenant onde ausentes.

### Próximas integrações
- Persistência transacional Protocolo + GED + Workflow.
- Outbox worker com reprocessamento operacional.
- Busca e relatórios por fonte real com máscara LGPD.

### Próxima sprint recomendada
1. Aplicar migrations em ambiente de homologação.
2. Rodar schema report e revisar gaps restantes.
3. Implementar services reais por fluxo, começando por Protocolo + GED + Workflow.
4. Cobrir permissões finas e auditoria de exportações/downloads.
5. Executar smoke test e POC com checklist manual.

## Próxima sprint recomendada após RC 1.0.0-rc.2

1. Executar validação completa em ambiente com .NET SDK e Docker.
2. Corrigir falhas reais de build/test/smoke/menu/console encontradas na homologação.
3. Ampliar testes automatizados de serviços transversais e permissões por módulo.
4. Promover módulos parciais somente após persistência, auditoria, LGPD e relatórios comprovados.
5. Coletar evidências visuais da POC e publicar pacote de release.


## Próxima sprint sugerida — Homologação final e release candidate
- Homologação final dos fluxos externos.
- Release candidate e congelamento de escopo.
- Empacotamento de produção e hardening.
- Documentação comercial e guia de implantação.
- Matriz de aderência por edital.
- Testes E2E automatizados.
- Performance avançada e carga em endpoints críticos.
- Deploy em ambiente real com observabilidade ativa.
