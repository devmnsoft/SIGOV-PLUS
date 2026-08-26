# RC50.68 — desbloqueio de gates e fundação transversal

## Escopo entregue

- migration PostgreSQL aditiva e idempotente para evidência transversal e fila
  de sincronização/outbox;
- identidade `bigint generated always as identity`, `tenant_id` e `entidade_id`
  obrigatórios nas novas estruturas;
- evidência com tipo, origem, relação validável, descrição, instante, coordenadas
  opcionais, hash SHA-256 opcional, responsável, classificação LGPD, auditoria e
  referência opcional ao GED;
- fila com unicidade idempotente por tenant, entidade, origem e chave, payload
  JSON, status, tentativas, erro sanitizado e instantes do ciclo;
- contratos C# reutilizáveis para evidência, sincronização e seleção relacionada;
- utilitário de saída CSV que neutraliza valores iniciados por `=`, `+`, `-` e `@`;
- manifest, checksums e seis artefatos consolidados sincronizados.

Não foram criados worker, upload, chamada externa, POC, mock, fallback de dados,
catálogo hardcoded, tela ou módulo. O GED existente é apenas referenciado.

## Autorização e módulos

Esta entrega não altera o avaliador de autorização nem cria permissões. Módulo,
perfil, permissão e parâmetro continuam sendo resolvidos pelo banco e em modo
fail-closed. A revisão estática confirmou que nenhum item FUNC21–FUNC24 foi
adicionado ao menu. A conferência dinâmica de permissões FUNC01–FUNC20 depende
de PostgreSQL e está registrada como bloqueada abaixo.

## Gates executados

| Gate | Resultado | Evidência |
| --- | --- | --- |
| referência/branch | PASS | trabalho iniciado em `87f23e2` na branch solicitada |
| manifest JSON | PASS | `python3 -m json.tool database/postgres/migrations/manifest.json` |
| checksums | PASS | SHA-256 de todas as migrations comparado ao manifest |
| scripts consolidados | PASS | quatro baselines iguais e dois scripts development iguais |
| whitespace Git | PASS | `git diff --check` |
| build .NET 10 | BLOCKED | executável `dotnet` não está instalado (`command not found`) |
| PostgreSQL 16/idempotência | BLOCKED | executável `psql` não está instalado e não foi fornecido banco de homologação |
| subida e smoke de rotas | BLOCKED | dependem do runtime .NET e de PostgreSQL/credenciais locais |

BLOCKED não foi convertido em sucesso simulado. Os gates devem ser reexecutados
em ambiente com SDK 10.0.100, PostgreSQL 16 e `ConnectionStrings__DefaultConnection`.

## Pendente

- executar build Release, aplicação e reaplicação do baseline em PostgreSQL 16;
- executar smoke anônimo e autenticado das rotas administrativas principais;
- validar dinamicamente unicidade e vínculos das permissões FUNC01–FUNC20;
- implementar FUNC21/SST360, FUNC22/Carbono360, FUNC23/Energia360 e
  FUNC24/Royalties360 somente em releases próprias, cada uma com persistência,
  autorização, regras no servidor, UI real e homologação.
