# Obras360 — FUNC13

## Escopo entregue

O FUNC13 evolui o cadastro anterior para uma operação integrada de cronograma físico-financeiro, diário digital, medição e memória de cálculo, decisões por alçada, eventos contratuais, ocorrências, não conformidades, ordens de serviço, geoevidências e publicação de transparência. O dashboard consulta exclusivamente o PostgreSQL e os repositórios usam Dapper/Npgsql com parâmetros e contexto de tenant/entidade.

## Persistência

A migration `20260826160000_exp13_obras360_func13.sql` complementa as tabelas existentes e cria itens de cronograma, equipes/equipamentos/clima do diário, memória e aprovação de medição, aditivo, reajuste, reequilíbrio, não conformidade, vínculo com `evidencia_transversal` e publicação. Todas as PKs novas são `bigint identity`; valores e percentuais possuem checks; índices cobrem contexto, obra, contrato, status, competência, prazo e data.

## Jornadas e regras

- A obra é escolhida em lista carregada do banco; não há digitação manual de ID.
- POSTs possuem antiforgery, validação server/client e recarregam as opções quando inválidos.
- Medições aprovadas somente são homologadas se respeitarem o saldo contratado/orçamentário; a homologação cria integração financeira idempotente e auditoria.
- Rejeição/cancelamento requer justificativa. Eventos contratuais não alteram automaticamente o contrato.
- CSV é segregado por tenant/entidade, auditado e neutraliza fórmulas (`=`, `+`, `-`, `@`, tab e CR).
- Geoevidência referencia a autoridade transversal `evidencia_transversal`; coordenadas e SHA-256 são validados naquele contrato.
- Publicação armazena apenas resumo público e visibilidade, sem projetar dados protegidos.

## Integrações reais

FUNC03 é referenciado por `contrato_id`; Financeiro por liquidação/pagamento; FUNC20 por convênio/emenda; Fiscaliza360 permanece autoridade sobre ordens e vistorias; GED é apenas referência documental opcional; Transparência recebe uma publicação sanitizada. Nenhuma dessas integrações simula sucesso.

## Permissões

`OBRAS_DASHBOARD_VIEW`, `OBRAS_CRONOGRAMA_VIEW`, `OBRAS_CRONOGRAMA_MANAGE`, `OBRAS_DIARIO_VIEW`, `OBRAS_DIARIO_MANAGE`, `OBRAS_MEDICAO_VIEW`, `OBRAS_MEDICAO_MANAGE`, `OBRAS_MEDICAO_APPROVE`, `OBRAS_ADITIVO_MANAGE`, `OBRAS_OCORRENCIA_MANAGE`, `OBRAS_NAO_CONFORMIDADE_MANAGE`, `OBRAS_TRANSPARENCIA_VIEW` e `OBRAS_RELATORIO_EXPORT`.

## Limites técnicos

Não foi criado upload: o módulo mantém somente vínculo e metadados da evidência transversal. Processamento externo de liquidação, pagamento, GED e portal depende dos adaptadores oficiais; falhas devem permanecer explícitas e com mensagem sanitizada. Não foram implementados GED como módulo principal, DefesaCivil360, Ativos360, Cidadão360, Jurídico360 ou FUNC21–FUNC24.

## Fechamento CORR13 (2026-08-26)

O fechamento revisou os read models materializados pelo Dapper (construtor padrão e aliases explícitos), restringiu a edição genérica pela permissão específica de cada recurso e tornou a lista de status dependente do fluxo. Formulários preservam `ModelState`, recarregam obras e estados válidos, exibem validação de campo e não expõem JSON nem identificadores técnicos para digitação. Os CSVs agora reaplicam busca, status e período da consulta, mantendo neutralização contra fórmulas e auditoria.

A migration corretiva `20260826170000_corr13_obras360_validacoes.sql` é idempotente, não remove objetos publicados e acrescenta checks defensivos de coordenadas, percentuais, períodos, saldos, conteúdo mínimo do diário e domínios de status/origem/severidade, além de índices contextuais. Ela foi incorporada aos baselines e ao manifest com SHA-256 verificado.

### Validação e limites desta execução

- `python3 -m json.tool database/postgres/migrations/manifest.json`: aprovado.
- sincronismo dos seis scripts completos e checksum da migration: aprovado.
- buscas estáticas de antiforgery, validação, IDs manuais e marcadores artificiais: executadas.
- BLOCKED: comando `dotnet build` não executado porque o executável `dotnet` não está instalado no ambiente.
- BLOCKED: comando `psql` não executado porque o executável `psql` não está instalado no ambiente e não há `ConnectionStrings__DefaultConnection` fornecida.
- BLOCKED: smoke básico das rotas MVC do Obras360 não executado porque o runtime `dotnet` não está instalado no ambiente.

Integrações financeiras, documentais, de convênios e Fiscaliza360 continuam somente sobre vínculos reais; nenhum adaptador, lançamento, contrato, evidência ou documento é simulado neste fechamento.
