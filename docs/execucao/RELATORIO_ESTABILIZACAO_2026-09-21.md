# Relatório de estabilização — 2026-09-21

## Escopo e critério de evidência

Esta rodada fez inventário estático do checkout e executou somente os gates
possíveis no ambiente. Documentação, migrations, controllers ou views isolados
não foram tratados como prova de homologação. Nenhum módulo foi promovido a
`PRONTO COM EVIDÊNCIA`, pois o ambiente não possui .NET 10, PostgreSQL 16,
PowerShell nem Docker e, portanto, não permitiu build, testes, aplicação das
migrations ou cenários HTTP autenticados.

O inventário encontrou os seis projetos de runtime (`Domain`, `Application`,
`Infrastructure`, `Api`, `Web` e `Worker`) e quatro projetos sob `tests`. O gate
estático percorreu 630 rotas da API. Esta rodada não alterou schema, migrations,
manifesto nem scripts consolidados.

## Resultado dos gates

| Gate | Resultado | Evidência objetiva |
|---|---|---|
| Restore/build das soluções | BLOQUEADO | `dotnet` não está instalado (`command not found`) |
| Testes unitários, API e integração | BLOQUEADO | dependem do SDK .NET 10 e dos binários de Release |
| Clean install, reaplicação e upgrade | BLOQUEADO | `psql` e Docker não estão instalados; não há conexão descartável configurada |
| Manifesto de migrations | PASS estático | JSON bem-formado por `python3 -m json.tool` |
| Shell dos fluxos locais | PASS estático | `bash -n` nos scripts de start, aplicação, promoção e homologação |
| Colisões no script consolidado | PASS estático | o gate foi corrigido para não confundir `ALTER TABLE sigov.%I` dinâmico com relação estática |
| Rotas API duplicadas | PASS estático | nenhuma colisão direta entre 630 rotas |
| Artefatos versionados | PASS estático | `check-tracked-artifacts.sh` não encontrou artefatos proibidos |
| Smoke HTTP e telas | BLOQUEADO | API/Web não puderam iniciar sem SDK; todos os endpoints retornaram HTTP `000` |
| Isolamento de dois tenants, permissão, auditoria e CSV | NÃO VERIFICADO | exigem runtime, identidades autorizadas e PostgreSQL descartável |

## Matriz final obrigatória

| Módulo | O que já existia | O que foi corrigido | O que foi evoluído | O que permanece parcial | Evidência | Risco | Próximo passo |
|---|---|---|---|---|---|---|---|
| Core / IAM / contexto | Cookie compacto, snapshot request-scoped, avaliador persistido e troca de contexto | Nada nesta rodada | Inventário e bloqueios registrados sem simular PASS | Login, revogação, troca de contexto e isolamento ponta a ponta | Projetos e código presentes; runtime não executado | Alto | Executar Gate A e cenário A/B com dois tenants |
| SaaS / SuperAdmin | Catálogo, entitlements, tenants e fluxos administrativos persistidos | Nada nesta rodada | Classificação mantida como `PARCIAL` | Planos, cobrança, bloqueios, auditoria e menu contratado em runtime | Matriz oficial e inventário estático | Alto | Homologar suspensão, inadimplência e negação a cliente comum em RC própria |
| Patrimônio / Ativos | Incorporação, termos e transferência com aceite | Nada nesta rodada | Gate de colisões deixa de emitir falso positivo por SQL dinâmico | Concorrência, OS ativa, fotografia e custódia em banco real | Migration e camadas presentes; sem PostgreSQL | Alto | Homologar recebimento até aceite e retry |
| Almoxarifado | Estoque, requisições, entregas, transferências e reposição | Nada nesta rodada | Gate estático estabilizado | Saldo concorrente, recebimento parcial e rollback | Estruturas presentes; sem execução transacional | Alto | Testar locks, saldo e idempotência em PostgreSQL 16 |
| Compras / Licitações / Contratos | Processos, recebimento parcial e pontes de estoque/patrimônio | Falso positivo do gate sobre a constraint LicitaPro | Validação estática volta a distinguir DDL estático de `format()` | Alçadas, pedido canônico, aceite/rejeição e integração ponta a ponta | Gate de colisões PASS; banco não aplicado | Alto | Clean install, reapply e recebimento 6+4 sem duplicidade |
| Frotas / Manutenção | Veículos, uso, abastecimento, OS e baixa de peças | Nada nesta rodada | Somente inventário | Autorização, concorrência, integração e isolamento runtime | Código existente; sem smoke | Alto | Validar OS, saldo insuficiente e rollback |
| Educação | Cadastros, ingresso/vagas, matrícula, frequência e avaliações | Nada nesta rodada | Somente inventário | Transferência, correção auditada, concorrência de vaga e portal | Migration/API/UI existentes; sem teste de banco | Alto | Testar última vaga concorrente e ausência sem presunção |
| Saúde / ACS | Unidades, equipes, pacientes, agenda, atendimento, vacinação, farmácia e regulação | Nada nesta rodada | Somente inventário | Território ACS, visita idempotente, LGPD e deduplicação | Camadas presentes; sem identidades/runtime | Alto | Homologar território permitido/negado e CSV mascarado |
| Saneamento / Meio ambiente | Cadastros, leitura/fatura/OS, laboratório e fiscalização | Nada nesta rodada | Somente inventário | Jornada comercial/operacional, GIS real e escopo territorial | Estruturas presentes; sem adapter/runtime | Alto | Validar leitura, faturamento e fiscalização em dois tenants |
| Financeiro / Orçamento / Tesouraria | Estruturas e serviços parciais de receita, despesa e tesouraria | Nada nesta rodada | Somente inventário | Regras fiscais parametrizadas, conciliação e prestação de contas | Não houve prova de cálculo ou banco | Crítico | Não integrar empenho antes da homologação canônica |
| RH / Folha / Portal | Cadastros, contratos e serviços parciais | Nada nesta rodada | Somente inventário | Cálculo, competência, autorização e portal integrados | Código presente; testes bloqueados | Crítico | Homologar regras parametrizadas e segregação |
| Protocolo / Processos / Ouvidoria / e-SIC | Persistência, controllers e fluxos parciais | Nada nesta rodada | Somente inventário | Histórico sensível, anexos, assinatura e auditoria de exportação | Sem acesso às rotas | Alto | Auditar visualização/exportação por perfil |
| Jurídico | Estrutura, serviços e telas parciais | Nada nesta rodada | Somente inventário | Filtros, histórico sensível, impressão e anexos | Sem runtime autenticado | Alto | Executar matriz permitido/negado e PDF sem campos nulos |
| GED / InovaGED | Estrutura histórica | Nada nesta rodada | Nenhum avanço, conforme restrição da sprint | Todo avanço funcional permanece adiado | Escopo preservado | Médio | Manter para etapa final |

## Decisão

O estado global permanece **BLOQUEADO PARA HOMOLOGAÇÃO**. Os resultados
estáticos são úteis como pré-gate, mas não substituem build, testes,
PostgreSQL 16, rotas autenticadas, auditoria ou prova de isolamento multi-tenant.
