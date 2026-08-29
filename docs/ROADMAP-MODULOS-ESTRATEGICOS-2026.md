# Roadmap de módulos estratégicos 2026

> **Marco RC50.80 (29/08/2026):** o ciclo dos módulos abaixo entrou em
> fechamento integrado. Promoção permanece condicionada ao gate estático
> `scripts/validate-rc50-80.py`, build .NET 10, PostgreSQL 16 e smoke autenticado;
> indisponibilidade ambiental deve ser registrada como BLOCKED.

## Objetivo

Integrar ao SIGOV PLUS os produtos estratégicos SST 360, LicitaPro IA,
Fiscaliza360, Obras360, DefesaCivil360, Ativos360, Carbono360, Cidadão360,
Jurídico360, Energia360 e Royalties360 sem duplicar domínios já existentes e
sem apresentar planejamento, mock, fallback ou tela vazia como funcionalidade
de produção.

Este roadmap não promove a RC50.68, que permanece bloqueada até a execução
verde dos gates oficiais de runtime, PostgreSQL, smoke e CI.

## Estado da fundação RC50.68

A migration corretiva `20260826120000` consolidou somente os contratos mínimos
de evidência e sincronização idempotente. A evidência guarda metadados, contexto
tenant/entidade, geolocalização opcional, hash e referência opcional ao documento
real do GED; não guarda arquivo fictício. A fila registra payload, estado,
tentativas e datas, mas não instala worker nem chama serviço externo.

FUNC21, FUNC22, FUNC23 e FUNC24 permanecem **planejadas e indisponíveis**. Elas
não possuem entrada de menu nem devem ser inferidas a partir desta fundação.

## Matriz de integração

| Produto estratégico | Integração no SIGOV PLUS | Tipo de evolução |
| --- | --- | --- |
| LicitaPro IA | FUNC03 Compras, Licitações, Contratos e Atas | Expandir com Portal do Fornecedor, radar PNCP, qualificação e análise assistida |
| Fiscaliza360 | Núcleo transversal de fiscalização e campo | Compartilhar com FUNC13, FUNC14, FUNC18 e FUNC19 |
| Obras360 | FUNC13 Obras Públicas, Engenharia e Fiscalização | Expandir medições, diário, geoevidências, transparência e integração financeira |
| DefesaCivil360 | FUNC19 Defesa Civil e Guarda Municipal | Expandir risco, contingência, abrigos, recursos, alertas e resposta |
| Ativos360 | FUNC01 Patrimônio + FUNC04 Frotas | Expandir QR/NFC, inventário móvel, manutenção, custos e ciclo de vida |
| Cidadão360 | FUNC15 Ouvidoria, Atendimento ao Cidadão e e-SIC | Expandir Carta de Serviços, autosserviço, SLA e atendimento omnicanal |
| Jurídico360 | FUNC17 Procuradoria, Jurídico e Contencioso | Expandir processos, prazos, dívida ativa, acordos, pareceres e provisões |
| SST 360 | FUNC21 Segurança e Saúde no Trabalho | Novo módulo integrado a RH, Saúde, GED, Workflow e eSocial |
| Carbono360 | FUNC22 Clima, Carbono e ESG | Novo módulo integrado a Meio Ambiente, Agro, Frotas, Obras e BI |
| Energia360 | FUNC23 Cadeia de Petróleo, Gás e Energia | Novo módulo B2B/B2G de fornecedores, conteúdo local, SMS e logística |
| Royalties360 | FUNC24 Royalties e Participações Governamentais | Novo módulo integrado a Financeiro, Planejamento, Convênios e Transparência |

## Fundação compartilhada obrigatória

Todos os módulos devem reutilizar os serviços canônicos de tenant, entidade,
exercício, unidade, pessoas, fornecedores, usuários, autorização persistida,
auditoria, LGPD, GED, Workflow, notificações, outbox, georreferenciamento,
sincronização offline, relatórios e BI. Não criar cópias paralelas desses
cadastros ou catálogos.

Novas entidades persistidas usam `bigint generated ... as identity`,
`tenant_id` e `entidade_id` obrigatórios e `exercicio_id`/`unidade_id` quando o
domínio exigir. UUID legado permanece compatível. Acesso é fail-closed e a
fonte de autoridade de módulos, perfis, permissões e parâmetros é o banco.

## FUNC21 — SST 360

Escopo fechado:

- GRO/PGR, estabelecimentos, ambientes, cargos, atividades e grupos expostos;
- inventário de riscos físicos, químicos, biológicos, de acidentes,
  ergonômicos e psicossociais;
- critérios versionados de severidade, probabilidade, nível de risco e decisão;
- avaliações coletivas com anonimato, limiar mínimo de respondentes e bloqueio
  de qualquer relatório que permita identificar a resposta individual;
- planos de ação, responsáveis, prazos, custos, evidências e verificação de
  eficácia;
- CIPA, inspeções, incidentes, acidentes, quase acidentes, treinamentos,
  EPIs/EPCs e permissões de trabalho;
- referências controladas de PGR, PCMSO, ASO, LTCAT e PPP no GED;
- integração configurável com eventos SST do eSocial, sem simular transmissão;
- segregação reforçada de dados médicos e psicossociais, com auditoria de
  visualização e exportação.

O sistema apoia o processo e a documentação, mas não substitui diagnóstico
médico/psicológico nem a responsabilidade de profissional legalmente
habilitado.

## Expansões FUNC03/FUNC13/FUNC19/FUNC01/FUNC04/FUNC15/FUNC17

### LicitaPro IA em FUNC03

- importação versionada de fontes oficiais, incluindo PNCP quando configurado;
- deduplicação por identificador e origem, com data da última sincronização;
- Portal do Fornecedor, certidões, habilitação, documentos e vencimentos;
- radar de oportunidades e score explicável, nunca decisão automática;
- checklist de edital, agenda, proposta e gestão do contrato conquistado;
- indisponibilidade de fonte externa deve ser exibida, nunca substituída por
  dados fictícios.

### Fiscaliza360 transversal

- ordem, vistoria, checklist, equipe, roteiro, evidência, auto e notificação;
- aplicativo/PWA offline com fila idempotente de sincronização;
- foto, vídeo, coordenada, data, dispositivo, hash e cadeia de custódia;
- adaptadores por domínio para Obras, Meio Ambiente, Trânsito e Defesa Civil;
- autos e sanções permanecem sob a regra e permissão do módulo de origem.

### Obras360 em FUNC13

- cronograma físico-financeiro, diário digital e geoevidências;
- medições com itens, memória de cálculo, fiscalização e aprovação por alçada;
- bloqueio de medição acima do saldo contratual/orçamentário;
- aditivos, reajustes, reequilíbrio, ocorrências, não conformidades e ordens;
- integração rastreável com FUNC03, Financeiro, Convênios, GED e Transparência.

### DefesaCivil360 em FUNC19

**EXP19 implementada em 2026-08-26**, integrada ao FUNC19 com persistência, RBAC, telas MVC/Razor, relatórios CSV e integrações transversais. A homologação ambiental permanece condicionada aos gates .NET 10/PostgreSQL 16.

- áreas e cenários de risco, população vulnerável e planos de contingência;
- ocorrências, níveis de resposta, equipes, abrigos, vagas, estoque e doações;
- rotas de evacuação, recursos, embarcações e comunicação à população;
- fontes meteorológicas/hidrológicas identificadas e estado de atualização;
- dados de pessoas abrigadas protegidos por LGPD e necessidade operacional.

### Ativos360 em FUNC01/FUNC04

- QR Code/NFC, inventário móvel, cadeia de responsabilidade e divergências;
- manutenção preventiva/corretiva, custos, documentos e alertas;
- veículos, máquinas e embarcações ligados ao patrimônio canônico;
- consumo, abastecimento e suspeita de anomalia sem acusação automática;
- integração com Almoxarifado, Compras, Contratos e Ordem de Serviço.

### Cidadão360 em FUNC15

- Carta de Serviços, autosserviço, protocolo e acompanhamento multicanal;
- Ouvidoria, e-SIC/LAI, agendamento, encaminhamento e SLA;
- localização consentida da demanda e comunicação acessível;
- pesquisa de satisfação, base de conhecimento e transparência agregada;
- sigilo, anonimato e restrição por finalidade quando aplicável.

### Jurídico360 em FUNC17

- processos judiciais/administrativos, partes, prazos, audiências e intimações;
- pareceres, acordos, obrigações, custas, dívida ativa e provisões;
- documentos e modelos versionados pelo GED;
- alçadas, impedimentos, sigilo processual e auditoria de acessos;
- integrações externas somente quando configuradas e homologadas.

## FUNC22 — Carbono360

- organizações, instalações, fontes e atividades emissoras;
- inventários dos escopos 1, 2 e 3 por período e unidade;
- fatores de emissão versionados com fonte, vigência e unidade de medida;
- cálculo reproduzível, memória de cálculo e trilha de alterações;
- metas, iniciativas, custos, reduções verificadas e evidências;
- indicadores ESG e relatórios com estado rascunho/revisado/aprovado;
- integração com Agro, Frotas, Obras, Meio Ambiente, Energia e BI;
- não emitir certificação ou crédito de carbono automaticamente.

## FUNC23 — Energia360

- passaporte e qualificação de fornecedores de petróleo, gás e energia;
- famílias de fornecimento, capacidade, instalações, equipamentos e regiões;
- requisitos jurídicos, econômicos, técnicos, qualidade, SMS e ESG;
- documentos, certificados, seguros, responsáveis e alertas de vencimento;
- diagnóstico de prontidão, lacunas e plano de adequação;
- radar de oportunidades por fontes permitidas e integrações configuradas;
- conteúdo local com origem, memória de cálculo, evidências e auditoria;
- mobilização, treinamentos, exames, escalas e bloqueio por requisito vencido;
- logística portuária, marítima e fluvial, cargas, contêineres e embarcações;
- contratos, medições, SLA, inspeções, incidentes e desempenho do fornecedor.

## FUNC24 — Royalties360

- fontes ANP versionadas, competências, campos, produção e beneficiários;
- importação idempotente, conciliação e histórico de retificações;
- previsão claramente separada de valor realizado/conciliado;
- royalties, participações especiais e demais receitas identificadas por tipo;
- vinculação a planejamento, orçamento, projetos e políticas públicas;
- alertas de variação, cenários e memória dos parâmetros de simulação;
- portal de transparência sem exposição de dados protegidos;
- nenhuma projeção pode ser tratada como lançamento contábil confirmado.

## UX, validação e operação

- Interface futurista, sóbria, calma, consistente, responsiva e acessível.
- Nenhuma tela operacional solicita identificador técnico ao usuário.
  Relacionamentos usam dropdown pesquisável, autocomplete validado, radio,
  checkbox, tabela de seleção ou assistente em etapas.
- Validação no cliente melhora a experiência, mas a regra de negócio e a
  autorização são sempre validadas no servidor.
- Formulários usam antiforgery, mensagens úteis, preservação segura de dados e
  prevenção de envio duplicado.
- Logs não expõem dados médicos, psicossociais, documentos, tokens ou outros
  dados pessoais completos.
- Menu e catálogo exibem apenas módulos contratados, habilitados e permitidos.

## Sequência de entrega

1. Promover a RC50.68 somente após gates reais verdes.
2. Consolidar contratos transversais de fiscalização, evidências, GED, outbox,
   georreferenciamento e offline sem duplicar tabelas.
3. Fechar as expansões dos módulos existentes, uma FUNC por release.
4. Implementar e homologar FUNC21 SST 360.
5. Implementar e homologar FUNC22 Carbono360.
6. Implementar e homologar FUNC23 Energia360.
7. Implementar e homologar FUNC24 Royalties360.

Cada FUNC exige migration idempotente, manifest e scripts completos
sincronizados, seed fictícia idempotente, Dapper parametrizado, regras de
domínio, serviços, API, MVC/Razor real, RBAC persistido, auditoria, LGPD,
relatórios, documentação, execução dos testes existentes e smoke autenticado.
Nenhuma FUNC é concluída apenas por documentação ou por presença de rota.

## EXP03 — LicitaPro IA integrado ao FUNC03

Entregue como expansão de Compras, Licitações, Contratos e Atas: fonte oficial configurável, importação versionada, radar, habilitação documental, análise assistida explicável, agenda, alertas, contrato conquistado, relatórios e auditoria. Integração externa permanece indisponível/não configurada até configuração operacional válida, sem simulação.

### CORR03 — LicitaPro IA / FUNC03

Fechamento técnico concluído no escopo do FUNC03: validações, MVC/Razor responsivo, filtros, CSV seguro, auditoria e integridade PostgreSQL. Fiscaliza360 e FUNC21–FUNC24 permanecem fora desta entrega.

## EXP-FISCALIZA360 transversal

Núcleo transversal de fiscalização e campo estruturado para FUNC13, FUNC14, FUNC18 e FUNC19, com ordens, vistorias, checklists, equipes, roteiros, autos, evidências, outbox, auditoria e relatórios. Integração externa offline permanece bloqueada até existir adaptador oficial.

## EXP13 — Obras360 / FUNC13

Obras360 foi promovido a expansão operacional: cronograma físico-financeiro, diário, geoevidências transversais, medições e aprovação, eventos contratuais, conformidade, ordens, transparência e rastreabilidade financeira. Detalhes e limites estão em `docs/OBRAS360-FUNC13.md`. As expansões DefesaCivil360, Ativos360, Cidadão360, Jurídico360 e FUNC21–FUNC24 permanecem fora deste incremento.

### CORR13 — fechamento do Obras360

Concluída a revisão defensiva do FUNC13: materialização Dapper, RBAC por recurso, formulários sem identificador técnico/JSON manual, estados de negócio controlados, CSV filtrado e migration corretiva sincronizada. Integrações permanecem dependentes de dados oficiais, sem fallback. Os módulos posteriores continuam fora deste incremento.

## CORR19 — DefesaCivil360/FUNC19

**Status técnico:** fechado para validação em ambiente dotnet/PostgreSQL. A correção consolida schema, validações operacionais, RBAC, LGPD, CSV seguro e formulários relacionais do DefesaCivil360. A entrega não avança outros módulos estratégicos e mantém integrações somente por referências canônicas reais. Evidências e comandos bloqueados estão registrados em `docs/entregas/CORR19-DEFESACIVIL360.md`.

## EXP08 — Ativos360 integrado

**Status técnico:** implementado para validação. O portal `/Ativos` consolida os módulos reais FUNC01, FUNC02 e FUNC04, com dashboard, navegação responsiva, complementos de ciclo de vida, RBAC persistido e integridade PostgreSQL. Não foram criados catálogos paralelos nem dados de fallback. Consulte `docs/ATIVOS360-FUNC08.md` e `docs/entregas/EXP08-ATIVOS360.md`.

## EXP04 — Cidadão360 integrado

**Status técnico:** implementado para validação. O portal `/Cidadao` evolui o FUNC15 e os contratos reais de processos, protocolo, Ouvidoria, pessoas e documentos. Entrega catálogo público persistido, solicitação autenticada, protocolo/verificador, timeline, área do cidadão, dashboard e design mobile-first. Upload, Gov.br, SMS e WhatsApp não são simulados sem adaptador oficial. Consulte `docs/CIDADAO360-FUNC04.md` e `docs/entregas/EXP04-CIDADAO360.md`.

## EXP06 — Jurídico360 integrado

**Status técnico:** implementado para validação. O FUNC17 foi ampliado com carteira, execução fiscal vinculada à dívida ativa/CDA, prazos, agenda, consultivo, documentos, acordos, precatórios/RPV, publicações, risco, auditoria, permissões e interface responsiva. Integrações judiciais externas permanecem explicitamente não configuradas sem adaptador oficial. Consulte `docs/JURIDICO360-FUNC06.md` e `docs/entregas/EXP06-JURIDICO360.md`.

## EXP09 — SST360 (entregue)

Saúde ocupacional e segurança do trabalho integradas ao RH, com ASO, riscos, programas legais, EPIs, treinamentos, CAT, investigação, PPP e monitor eSocial SST. Persistência, permissões LGPD, dashboard e experiência MVC/Razor entregues na migration `20260827150000`.

## EXP23 — Energia360 / FUNC23

**Status técnico:** implementado para validação. Gestão energética contextual com UCs, medição, faturas, contratos/demanda, iluminação, geração distribuída, créditos, eficiência, alertas, CSV protegido e integração Carbono360 baseada exclusivamente em fator persistido. Consulte `docs/ENERGIA360-FUNC23.md` e `docs/entregas/EXP23-ENERGIA360.md`.

## EXP24 — Royalties360 / FUNC24

**Status técnico:** implementado para validação. Governança de parâmetros normativos versionados, origens, previsão e realização, repasses e conciliação, planos de aplicação, projetos integrados, execução financeira referenciada, transparência aprovada, alertas e CSV seguro. Consulte `docs/ROYALTIES360-FUNC24.md` e `docs/entregas/EXP24-ROYALTIES360.md`.

## EXP13 — Saneamento360 / SIGCOS

Evolução comercial e operacional concluída em 2026-08-29: rotas MVC canônicas, governança LGPD, hidrômetros, revisão, faixas tarifárias, cobrança, campo, GIS tabular e qualidade da água. Integrações financeiras, patrimoniais, de protocolo e mapa permanecem condicionadas aos provedores reais configurados. Detalhes em [SANEAMENTO360-FUNC13](SANEAMENTO360-FUNC13.md).

## EXP11 — Saúde360 + ACS360 (2026-08-29)

Entregue o núcleo territorial e de campo do FUNC11: ACS, domicílios, famílias, indivíduos, visitas, produtividade, offline idempotente, ocorrências, riscos, staging e-SUS/SISAB e quatro vigilâncias. Permanecem bloqueados o app Android/câmera antifraude e a transmissão ministerial por ausência dos contratos oficiais versionados.

## EXP25 — GED360 / InovaGED Inteligente

Núcleo entregue: governança documental e arquivística, protocolo, OCR rastreável, busca PostgreSQL, workflow, temporalidade, acervo físico, eliminação controlada, auditoria LGPD e vínculos transversais. Ativação de OCR e assinatura depende de provedores reais configurados; nenhuma resposta é simulada.
