# RC50.47 — Tributário Avançado

## Entrega
Foram adicionados quatro núcleos incrementais: carnês/boletos/DAM com produção e entrega; portal do contribuinte; fiscalização ISSQN; e NFS-e, Livro Eletrônico e DES-IF em caráter preparatório. A persistência usa Dapper, SQL parametrizado, allowlist de tabelas, tenant obrigatório, soft delete, `CorrelationId` e auditoria JSONB.

## Banco e integrações
As migrations `20260817150000` a `20260817153000` criam 54 tabelas idempotentes, índices tenant/status e restrições essenciais. Manifest e scripts consolidados foram atualizados. Nenhuma integração FEBRABAN, bancária, PIX, ABRASF, Simples Nacional ou DES-IF é declarada oficial; os fluxos correspondentes permanecem preparatórios.

## API e telas
A API cobre dashboards, emissão, CSV de dados variáveis, produção, ocorrências de entrega, certidões/guias/parcelamentos, ordem fiscal, diligência, notificação, auto/defesa/julgamento, notas, livro e declarações. As páginas Web canônicas usam uma composição institucional responsiva com KPI, filtros, tabela, empty state, LGPD e timeline de auditoria; o menu oferece atalhos apenas para rotas implementadas.

## Regras, LGPD e auditoria
Cancelamento e nova tentativa exigem justificativa; auto exige descrição e valor; nota preparatória exige valor positivo. Listagens não selecionam documentos pessoais, CSV não inclui CPF/CNPJ/endereço e todas as consultas são isoladas por tenant. Mutações persistem usuário, correlação e evento em `auditoria`.

## Validação e pendências
Manifest, verificador de índices parciais, busca de raw strings/`SELECT *` e verificador de rotas foram executados. PostgreSQL, SDK .NET, Swagger e login ficaram pendentes porque `psql` e `dotnet` não existem no ambiente. RC50.48 deve executar o smoke ponta a ponta, ampliar transições formais, conectar protocolos aos processos digitais e homologar permissões granulares por ação.
