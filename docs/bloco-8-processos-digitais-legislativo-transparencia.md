# Bloco 8 — Processos Digitais, Legislativo, GED, Assinaturas, Transparência e Atendimento

## Finalidade e fonte de escopo

Este documento consolida no planejamento do SIGOV+ o adendo formado pelos três novos documentos de referência. Ele substitui, para o próximo Bloco 8, o recorte anterior de “Protocolo/GED/Assinaturas/Atendimento”.

O adendo é fonte de requisitos e de planejamento, não evidência de funcionalidade já implementada. Nenhuma integração externa ou aderência legal deve ser anunciada como operacional antes de implementação, homologação e produção de evidências.

## Gate obrigatório: conclusão do RC50.41

O Bloco 8 **não deve ser implementado** até que o RC50.41 possua evidências para todos os critérios abaixo:

- migrations aplicadas com sucesso em PostgreSQL;
- build de runtime aprovado;
- Swagger abrindo e expondo os contratos esperados;
- login de `admin` e `superadmin` funcionando;
- rotas principais do Bloco 7 validadas, sem regressão.

Enquanto o gate estiver aberto, são permitidos somente refinamento de escopo, análise, modelagem conceitual e preparação do backlog. A criação de migrations, tabelas, DTOs, repositories, services, controllers ou views do Bloco 8 deve aguardar o registro formal da conclusão do RC50.41.

## Composição funcional

### 1. Protocolo e Processos Digitais

- protocolo e acompanhamento por número, interessado e situação;
- consulta e gestão de processos digitais;
- movimentações, histórico, prazos, responsáveis, anexos e consulta pública;
- integração futura com Workflow, Alertas, Qualidade de Dados, Relatórios Executivos e Auditoria.

### 2. GED, Arquivo Digital e Assinaturas

- documentos administrativos e legislativos;
- arquivos físicos e digitais, acervo legislativo e migração de acervo;
- revisão e publicação para consulta online;
- versionamento da legislação municipal;
- pesquisa, metadados, temporalidade, retenção e trilha de auditoria;
- assinatura eletrônica avançada conforme a Lei nº 14.063/2020, com identificação do signatário, integridade, rastreabilidade, evidências técnicas e auditoria;
- assinatura sequencial e paralela como evolução posterior;
- ICP-Brasil somente quando legalmente exigida e após existir infraestrutura real homologada.

### 3. Legislativo/Câmara, Sessões, Votação e Normas

- gestão e publicação de proposições;
- tramitação legislativa e histórico;
- sessões plenárias;
- pauta, expediente e ordem do dia;
- atas e pareceres;
- votação eletrônica e resultado de votação;
- normas, publicação e consulta pública.

As regras de quórum, votação, numeração, autoria, tramitação e publicação deverão ser parametrizáveis e validadas com a entidade antes da implementação do fluxo oficial.

### 4. Transparência, Diário Oficial, e-SIC, Ouvidoria e Atendimento

#### Portal institucional

- publicações, transparência e acesso à informação;
- consulta legislativa e de normas;
- acompanhamento de protocolos;
- separação explícita entre área pública e área administrativa.

#### e-SIC

- abertura e classificação do pedido;
- controle de prazo legal, prorrogação, resposta e recurso;
- histórico auditável;
- indicadores e relatório de transparência.

#### Ouvidoria

- manifestação nas categorias denúncia, reclamação, elogio, sugestão e solicitação;
- classificação, resposta e controle de prazo;
- sigilo, proteção LGPD e acesso restrito conforme o perfil;
- indicadores operacionais e gerenciais.

#### Diário Oficial próprio

- criação de edição, envio de matérias, revisão e publicação;
- calendário parametrizável para dias úteis, finais de semana e feriados;
- preservação do arquivo original;
- autenticidade por hash, QR Code e código de validação;
- resumo diário e painel de publicações;
- preparação de contrato e dados para futuro resumo em áudio, sem implementar geração de áudio nesta etapa.

#### Portal de Transparência / PNTP

- painel de critérios PNTP/ATRICON;
- itens de conformidade, pendências, percentual de atendimento e evidências;
- atualização de dados e exportação;
- dashboard de governança e controle social;
- preparação arquitetural para PNCP, sem afirmar integração oficial antes de implementação e homologação.

## Produto responsivo e preparação para mobile

As áreas pública e administrativa devem ser responsivas. O produto deve preparar APIs versionadas e seguras para:

- consulta de processos e sessões;
- acompanhamento de protocolos;
- notificações;
- consumo futuro por Android e iOS.

Não faz parte deste bloco inicial criar aplicativo nativo. Compatibilidade Android/iOS é requisito de produto e de contratos de API, não declaração de que um aplicativo já existe.

## Migração, conversão e validação de dados

O plano de implantação deve contemplar:

- migração de processos, documentos e acervo legislativo;
- conversão rastreável, preservando origem e identificadores legados quando aplicável;
- validação quantitativa e qualitativa dos dados;
- relatório de inconsistências e estratégia de saneamento;
- termo de aceite;
- operação assistida após a carga homologada.

Migração nunca deve apagar ou sobrescrever silenciosamente o legado. Reprocessamentos precisam ser idempotentes e produzir correlação e evidências.

## Requisitos transversais de dados e segurança

Toda nova tabela do Bloco 8 deverá conter, no mínimo:

- `tenant_id`, com isolamento obrigatório por tenant;
- dados de auditoria e autoria compatíveis com a ação;
- `correlation_id`, propagado entre API, Application, persistência, integrações e logs;
- `is_deleted`, para exclusão lógica;
- `created_at`, com data/hora consistente.

Quando aplicáveis, a modelagem também deverá prever `updated_at`, responsável pela atualização, classificação LGPD, nível de sigilo, origem, versionamento, hash e regras de retenção. Logs e exportações não podem expor conteúdo sigiloso ou dados pessoais sem autorização.

## Ordem de implementação após o gate

1. Modelar tabelas para processo digital, protocolo, GED, assinatura, diário oficial, transparência, e-SIC, ouvidoria e legislativo.
2. Criar migrations PostgreSQL idempotentes, aditivas e seguras.
3. Criar DTOs com nomes únicos e contratos sem ambiguidades entre módulos.
4. Implementar repositories Dapper com filtro obrigatório por `tenant_id` e exclusão lógica.
5. Implementar services da camada Application e suas regras de autorização e auditoria.
6. Expor controllers de API, incluindo contratos versionados preparados para mobile.
7. Implementar controllers Web.
8. Criar views premium, responsivas e acessíveis para áreas pública e administrativa.
9. Integrar com Workflows, Alertas, Qualidade de Dados, Relatórios Executivos e Auditoria.
10. Validar migrations, build, Swagger, login e ausência de regressão nas rotas do Bloco 7.

Cada módulo deve avançar em fatias verticais homologáveis. A existência de schema ou tela isolada não autoriza classificar o fluxo como concluído.

## Fora de escopo nesta etapa

- aplicativo mobile nativo;
- assinatura ICP-Brasil real sem infraestrutura e provedor homologados;
- alegação de integração oficial com PNCP, Gov.br ou ICP-Brasil sem implementação;
- OCR avançado antes da estabilização do GED básico;
- criação de testes durante este adendo documental;
- alterações que quebrem ou antecipem regressões no Bloco 7;
- tabelas que não atendam aos campos transversais obrigatórios.

## Critérios de aceite do futuro Bloco 8

- gate RC50.41 documentado como aprovado antes do primeiro artefato de implementação;
- migrations reaplicáveis no PostgreSQL e sem operações destrutivas não autorizadas;
- isolamento por tenant, LGPD, sigilo, correlação e auditoria demonstrados;
- DTOs únicos e contratos documentados no Swagger;
- login administrativo e autorização por perfil validados;
- rotas públicas sem exposição de dados administrativos ou sigilosos;
- integrações externas rotuladas honestamente como preparadas, configuradas ou homologadas;
- build de runtime aprovado e rotas do Bloco 7 novamente verificadas;
- migração acompanhada por relatório de inconsistências, aceite e operação assistida quando houver legado.
