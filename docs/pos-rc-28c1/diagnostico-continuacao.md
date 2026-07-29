# Diagnóstico de continuação — Pós-RC 28C.1

## Base Git

- Data da verificação: 2026-07-29 (UTC).
- SHA base esperado: `2c97763215f449326744030402ed114931b937b9`.
- SHA real do checkout: `2c97763215f449326744030402ed114931b937b9`.
- O checkout não possuía `origin`. A tentativa de configurar o remote oficial e
  executar `git fetch origin` falhou com HTTP 403 no túnel de rede do ambiente.
- Consequentemente, `origin/main` não pôde ser resolvido. O checkout local coincide
  com o SHA esperado e foi usado sem alegar validação do estado remoto.
- Branch criada: `codex/pos-rc-28c1-continuacao-implementacao-real`.

## PR #147 e execuções de workflow

O commit local `2c97763215f449326744030402ed114931b937b9` é o merge do PR #147,
intitulado **Pós-RC 28C: schema sisgov e script PostgreSQL completo**. O diff do PR
disponível no repositório contém somente o diagnóstico inicial em
`docs/pos-rc-28c/diagnostico-inicial.md`; não contém implementação de schema.

Foram examinados localmente `.github/workflows/net10.yml` e
`.github/workflows/ci.yml`. O primeiro separa restore, builds Debug/Release e as
suítes unitária, API, integração e injeção de dependência. O segundo workflow ainda
consolida build e testes no job `build-test` e não contém todos os gates solicitados
para o schema `sisgov`.

A consulta remota ao PR #147, ao run 3 do workflow `net10` e ao CI mais recente não
foi autorizada (HTTP 401/403). Não há logs de restore, Debug ou Release, artifacts,
TRX ou binlogs desses runs no checkout. Portanto, nenhum resultado remoto foi
inferido ou declarado como aprovado.

## Portão 0

Foi corrigida a causa do CS8601 em `NpgsqlConnectionFactory`: a configuração nula é
rejeitada, a connection string nullable é validada antes de ser atribuída ao campo
não anulável, e não foi adicionada supressão ao código produtivo. Também foram
adicionados os cinco testes de contrato pedidos.

O comando obrigatório abaixo foi executado e falhou antes do restore:

```text
dotnet restore sigov.sln --locked-mode
/bin/bash: line 1: dotnet: command not found
```

O SDK .NET não está instalado. PostgreSQL (`psql`) e Docker também não estão
disponíveis neste ambiente. Em cumprimento à regra de não avançar enquanto um
bloqueio anterior existir, as fases de banco, baseline, runtime, Docker e standalone
não foram iniciadas. Não há alegação de build, testes, idempotência, migração ou CI
verde.

## Pendências bloqueadas pelo ambiente

1. Disponibilizar .NET SDK 10 e acesso às dependências para concluir o Portão 0.
2. Disponibilizar acesso autenticado ao GitHub para auditar runs, logs e artifacts.
3. Disponibilizar PostgreSQL 16, Docker e PowerShell/Windows para os portões de banco,
   composição e standalone.
4. Após o Portão 0 ficar verde, implementar sequencialmente os demais portões sem
   substituir globalmente identificadores `Sigov` que não representam schema.
