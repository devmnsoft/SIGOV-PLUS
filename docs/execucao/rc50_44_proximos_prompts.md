# RC50.44 — Próximos prompts executáveis

Regras comuns: preservar working tree; C# 10; Dapper/SQL parametrizado; sem EF, `SELECT *`, SQL concatenado ou testes nesta fila salvo quando RC50.52 autorizar o fechamento; banco `postgres`, schema/search path `sigov`. Cada sprint entrega os quatro avanços, não apenas documentação.

## RC50.45 — Fechar Bloco 8
1. **Contexto:** governo digital reúne Protocolo/Processos, GED/Assinaturas, Legislativo e canais públicos.
2. **Já existe:** migrations e camadas `Bloco8`, controllers/views de Processos, Protocolo, GED, Assinaturas, Legislativo, Transparência, Diário, Ouvidoria e Atendimento.
3. **Falta:** provar persistência e transições ponta a ponta; consolidar e-SIC e publicação.
4. **Avanço 1 — estabilização:** aplicar manifest em banco limpo e parcialmente migrado; eliminar rota duplicada/500 e proteger dashboard quando objeto opcional faltar.
5. **Avanço 2 — funcionalidade:** protocolo gera processo, recebe documento GED, tramita, assina e publica; incluir consulta pública por código sem revelar conteúdo restrito.
6. **Avanço 3 — regra/LGPD/auditoria:** matriz de sigilo, hash/integridade, segregação tenant, transição autorizada e trilha de leitura/download/publicação com correlation id.
7. **Avanço 4 — UX:** unificar menu Bloco 8, timeline, filtros de prazo/status, empty/loading/toast e relatório de SLA/exportação.
8. **Arquivos prováveis:** `src/Sigov.Application/Bloco8/**`, `src/Sigov.Infrastructure/Bloco8/**`, `src/Sigov.Api/Controllers/*{Processo,Protocolo,Ged,Assinatura,Legislativo,Transparencia}*`, `src/Sigov.Web/Controllers/**`, `src/Sigov.Web/Views/{ProcessosDigitais,Protocolo,Ged,Assinaturas,Legislativo,Transparencia,DiarioOficial,Ouvidoria,AtendimentoDigital}/**`, migration RC50.45.
9. **Validação:** manifest, índice parcial, rotas, migrations, build warn-as-error, Swagger e login admin/superadmin; smoke do protocolo à publicação.
10. **Aceite:** fluxo persistido, idempotente e tenant-safe; sigilo efetivo; trilha consultável; telas responsivas; nenhum segredo.
11. **Relatório final:** mudanças, rotas, migrations/checksums, regras, evidências dos quatro avanços, comandos e pendências.

## RC50.46 — Fechar Bloco 9
1. **Contexto:** cadeia empresarial de CRM até OS/estoque/compras/produção.
2. **Já existe:** `CommercialRepository`, Ordem de Serviço, compras empresariais, comércio/estoque e indústria, com APIs e views parciais.
3. **Falta:** fechar lead→proposta→pedido→reserva/compra→OS/produção e dashboards coerentes.
4. **Avanço 1 — estabilização:** remover SQL dinâmico inseguro/consultas genéricas remanescentes do comércio, validar concorrência/version e objetos opcionais.
5. **Avanço 2 — funcionalidade:** confirmar pedido com reserva; falta gera requisição; serviço gera OS e produto fabricado gera ordem de produção, com estorno consistente.
6. **Avanço 3 — regra/auditoria:** idempotency key, bloqueio de estoque negativo, segregação de funções, mascaramento de cliente e histórico imutável de status.
7. **Avanço 4 — UX:** kanban comercial, painel de disponibilidade/ruptura, agenda de OS e produção, filtros/exportação e menu Bloco 9.
8. **Arquivos prováveis:** `Application/{Commercial,OrdemServico,ComprasEmpresariais,Industria}/**`, pares em `Infrastructure`, controllers API/Web e views `Comercial`, `Tecnico`, `ComprasEmpresariais`, `Industria`.
9. **Validação:** checks estáticos, migration/build/Swagger/login; smoke lead até execução e cancelamento.
10. **Aceite:** nenhuma duplicação por retry, saldo reconciliado, tenant/LGPD/auditoria comprovados e UX demonstrável.
11. **Relatório final:** quatro avanços, cenários executados, rotas/tabelas, riscos residuais e evidências.

## RC50.47 — Tributário Avançado
1. **Contexto:** arrecadação municipal amplia Dívida Ativa para emissão e fiscalização.
2. **Já existe:** núcleo Tributário/Dívida Ativa, portal contribuinte e migrations RC50.36.
3. **Falta:** carnê/boleto, autoatendimento, fiscalização ISSQN e contratos preparatórios NFS-e/DES-IF.
4. **Avanço 1 — estabilização:** reconciliar débitos/parcelas/status e tornar emissão idempotente; corrigir índices e datas monetárias.
5. **Avanço 2 — funcionalidade:** gerar carnê com parcelas/linha digitável simulada, consulta autenticada e protocolo de fiscalização ISSQN; criar importação preparatória validada sem integração oficial falsa.
6. **Avanço 3 — regra/auditoria:** cálculo determinístico de multa/juros, competência, prescrição sinalizada, acesso por contribuinte e auditoria de consulta/emissão/alteração.
7. **Avanço 4 — UX:** extrato responsivo, filtros por exercício/status, segunda via, demonstrativo PDF/CSV e painel de inadimplência sem dados pessoais abertos.
8. **Arquivos prováveis:** `Application/Tributario/**`, `Infrastructure/Tributario/**`, `Api/Controllers/TributarioController.cs`, `Web/Controllers/{Tributario,PortalContribuinte}Controller.cs`, views correspondentes, migration RC50.47.
9. **Validação:** manifest/checksum/índices, build, Swagger/login, emissão repetida e isolamento tenant/contribuinte.
10. **Aceite:** valores reproduzíveis, retry seguro, portal sem vazamento, fiscalização persistida e relatório exportável.
11. **Relatório final:** fórmulas, fluxos, endpoints, telas, SQL, evidências e integrações explicitamente simuladas.

## RC50.48 — Educação Avançada
1. **Contexto:** ampliar o núcleo escolar para logística, alimentação, biblioteca e indicadores.
2. **Já existe:** escola/turma/matrícula, diário e portal responsável, serviços/repos/migrations dos blocos 1 e 3.
3. **Falta:** transporte, merenda, acervo/empréstimo e custos/FUNDEB.
4. **Avanço 1 — estabilização:** sanear vínculos aluno/matrícula/ano letivo e dashboards tolerantes a tabelas opcionais.
5. **Avanço 2 — funcionalidade:** rota/ponto/aluno no transporte; cardápio com estoque/porção; empréstimo/devolução/reserva; lançamento de custo por unidade.
6. **Avanço 3 — regra/auditoria:** impedir aluno duplicado em rota, controlar alergia com acesso restrito, multa/bloqueio configurável e rastrear leitura de dado de menor.
7. **Avanço 4 — UX:** mapas preparatórios sem serviço externo obrigatório, calendário de cardápio, busca de acervo e painel FUNDEB/custo-aluno com exportação.
8. **Arquivos prováveis:** `Application/Educacao/**`, `Infrastructure/Educacao/**`, controllers `Educacao*`, views `Educacao/**`, migration RC50.48.
9. **Validação:** migrations/build/Swagger/login; smoke de cada fluxo e autorização responsável/escola.
10. **Aceite:** quatro domínios persistem, regras bloqueiam inconsistência, dados de menores protegidos e dashboards têm empty/loading.
11. **Relatório final:** matriz funcional, regras/LGPD, rotas/telas, evidências e pendências de mapas oficiais.

## RC50.49 — Saúde Avançada
1. **Contexto:** APS evolui ACS, visita, vacina, farmácia e regulação.
2. **Já existe:** núcleo Saúde/ACS e migration RC50.38 com API/Web.
3. **Falta:** sincronização offline, georreferência, carteira vacinal, dispensação e fila regulatória.
4. **Avanço 1 — estabilização:** corrigir dependências opcionais e consistência pessoa/família/unidade; definir conflito de sincronização e retentativa.
5. **Avanço 2 — funcionalidade:** pacote offline de microárea, visita com sync, dose/lote, dispensação por estoque e solicitação/priorização regulatória.
6. **Avanço 3 — regra/LGPD:** mínimo necessário offline, criptografia/expiração local contratual, consentimento/base legal, rastreio de prontuário, lote/validade e prioridade clínica auditável.
7. **Avanço 4 — UX:** mapa/agenda ACS, carteira vacinal, alertas de validade/estoque e fila de regulação acessível com filtros/empty/loading.
8. **Arquivos prováveis:** `Application/Saude/**`, `Infrastructure/Saude/**`, `Api/Controllers/SaudeControllers.cs`, `Web/Controllers/{Saude,Acs}Controller.cs`, views e migration RC50.49.
9. **Validação:** migrations/build/Swagger/login; sync duplicado/conflitante, lote vencido, acesso indevido e fila.
10. **Aceite:** offline idempotente, rastreabilidade clínica, estoque consistente, LGPD e quatro telas demonstráveis.
11. **Relatório final:** cenários offline, regras clínicas, segurança, endpoints, UI e limites não implementados.

## RC50.50 — Saneamento Avançado
1. **Contexto:** completar cadastro técnico com medição, faturamento, OS e qualidade.
2. **Já existe:** núcleo saneamento e integração com OS parcialmente disponível.
3. **Falta:** GIS, ciclo de leitura/fatura/arrecadação, corte/religação e laboratório.
4. **Avanço 1 — estabilização:** validar unidade/ligação/hidrômetro e tabelas opcionais; eliminar dupla cobrança por competência.
5. **Avanço 2 — funcionalidade:** geometria preparada, rota de leitura, cálculo/fatura/baixa, inadimplência→ordem de corte/religação e amostra→laudo.
6. **Avanço 3 — regra/auditoria:** tarifa/faixa/versionamento, exceções sociais, dupla aprovação de corte, cadeia de custódia da amostra e proteção do titular.
7. **Avanço 4 — UX:** mapa operacional, painel arrecadação, agenda de OS, série de parâmetros laboratoriais, filtros/exportação e alertas.
8. **Arquivos prováveis:** `Application/Saneamento/**`, `Infrastructure/Saneamento/**`, `Api/Controllers/SaneamentoControllers.cs`, `Web/Controllers/SaneamentoController.cs`, views e migration RC50.50.
9. **Validação:** migrations/build/Swagger/login; competência repetida, baixa, corte/religação e laudo fora do limite.
10. **Aceite:** ciclo financeiro conciliado, OS auditável, qualidade rastreável e painel sem depender de GIS externo.
11. **Relatório final:** fórmulas/tarifas, rotas/tabelas, evidências de fluxos, LGPD e integrações pendentes.

## RC50.51 — Segurança, LGPD e Auditoria Final
1. **Contexto:** endurecimento transversal antes do fechamento.
2. **Já existe:** permissões, auditoria, mascaramento/classificação LGPD e autenticação administrativa.
3. **Falta:** granularidade por ação, trilha uniforme de leitura, relatório do titular, incidentes/retenção/anonimização.
4. **Avanço 1 — estabilização:** inventariar endpoints sem policy/tenant e fechar bypasses; garantir que login/Swagger/bootstrap permaneçam funcionais.
5. **Avanço 2 — funcionalidade:** matriz administrável, relatório do titular assíncrono, registro/tratamento de incidente e jobs de retenção em dry-run/aprovação.
6. **Avanço 3 — regra/LGPD/auditoria:** deny-by-default, finalidade/base legal, trilha append-only, segregação, legal hold e anonimização irreversível após aprovação.
7. **Avanço 4 — UX:** console de permissões com diff, timeline de acesso, painel de incidentes/SLA e assistente de relatório/retensão acessível.
8. **Arquivos prováveis:** `Application/{Security,Lgpd,Auditoria}/**`, `Infrastructure/{Security,Lgpd}/**`, controllers `Seguranca`, `Lgpd`, `Auditoria`, views e migration RC50.51.
9. **Validação:** build/migrations/rotas/Swagger/login; matriz admin/usuário, cross-tenant, exportação e dry-run sem apagar dados.
10. **Aceite:** nenhum endpoint pessoal sem controle/trilha, relatório minimizado, retenção segura e telas auditáveis.
11. **Relatório final:** gaps fechados, matriz, ameaças, evidências, dados tratados e riscos aceitos.

## RC50.52 — Fechamento Técnico
1. **Contexto:** consolidar release após sprints funcionais.
2. **Já existe:** solution filter runtime, scripts de validação, health checks, logs e documentação extensa.
3. **Falta:** baseline de performance, observabilidade/alertas, pacote reproduzível, testes finais e runbook único.
4. **Avanço 1 — estabilização:** zerar P0/P1, warnings, conflitos, drift de migration e falhas de health/login/Swagger.
5. **Avanço 2 — funcionalidade:** empacotamento versionado com configuração externa, backup/restore e rollback operacional ensaiado.
6. **Avanço 3 — regra/auditoria:** retenção de logs, redaction, correlação ponta a ponta, SLO/alerta e evidência de acesso administrativo.
7. **Avanço 4 — UX/relatório:** status final, relatório de release/aceite, dashboards de observabilidade e manual operacional navegável.
8. **Arquivos prováveis:** solution/props, `scripts/**`, `src/*/Health/**`, logging/configuração, pipeline/container, `docs/operacao/**`; somente nesta sprint criar testes finais aprovados.
9. **Validação:** restore/build warn-as-error, migrations limpa/parcial, Swagger/login/rotas, smoke E2E, carga controlada, backup/restore e scan de segredos.
10. **Aceite:** pacote reproduzível, SLO observável, rollback documentado/executado, P0/P1 zero e aceite assinado.
11. **Relatório final:** versão/commit, artefatos/checksums, todos os comandos/resultados, métricas, limitações e go/no-go.
