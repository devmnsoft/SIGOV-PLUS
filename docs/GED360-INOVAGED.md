# GED360 / InovaGED Inteligente

## Arquitetura

O GED360 evolui o módulo GED/Protocolo do SIGOV-PLUS. O módulo usa o contexto autenticado (`tenant_id`, `entidade_id`, `exercicio_id` e `unidade_id`), autorização persistida, Dapper/Npgsql, auditoria e vínculos em vez de copiar documentos para módulos consumidores.

A migration `20260829140000_exp25_ged360_inovaged.sql` cria o núcleo arquivístico, importação, OCR rastreável, extração por regras, pesquisa salva, protocolo, workflow, assinatura pendente, acervo físico, eliminação, auditoria LGPD e vínculos de integração. Todas as chaves novas são `bigint identity` e a migration não remove estruturas legadas.

## Limites técnicos explícitos

- **OCR:** o job permanece `PENDENTE` até um motor real ser configurado. Resultado, confiança e revisão humana são persistidos; não é produzido texto fictício.
- **Extração inteligente:** regras e termos locais podem alimentar campos e sugestão de classificação. Nenhuma IA externa é chamada sem configuração.
- **Assinatura:** o sistema registra a solicitação. Apenas o retorno verificável de um provedor pode marcar `ASSINADA`.
- **Arquivo:** metadados podem ser registrados, mas conteúdo só é considerado armazenado quando houver `storage_key`, MIME, tamanho e SHA-256 reais.
- **Eliminação:** aprovação não apaga conteúdo. A execução física exige política explícita, permissão `GED_ELIMINACAO_APPROVE` e auditoria.

## Segurança e LGPD

Consultas são parametrizadas e sempre limitadas pelo tenant. A permissão `GED_DOCUMENTO_SENSIVEL_VIEW` separa acesso sensível; `ged_auditoria_acesso` registra usuário, ação, justificativa, IP, user-agent e correlation ID. Metadados sensíveis oferecem valor mascarado. Downloads e exportações devem registrar auditoria; CSV deve neutralizar células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno de carro.

## Operação

Rotas sob `/GED` oferecem dashboard, documentos, busca e áreas de importação, OCR/revisão, classificação, temporalidade, protocolo, tramitação, workflows, assinaturas, acervo, caixas, empréstimos, eliminações, integrações, auditoria e relatórios. Estados vazios são honestos e nunca substituem indisponibilidade de schema por dados de demonstração.

## Busca

`ged_documento.texto_busca` usa `tsvector` e índice GIN. A pesquisa usa `websearch_to_tsquery('portuguese', ...)`, além de filtros parametrizados por status e confidencialidade. `unaccent`/`pg_trgm` não são requisitos e não são instaladas silenciosamente.
