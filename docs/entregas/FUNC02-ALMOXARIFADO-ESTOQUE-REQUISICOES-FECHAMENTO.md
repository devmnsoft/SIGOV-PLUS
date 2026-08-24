# Fechamento FUNC02 — Almoxarifado

## Entrega

- **Migration:** `database/postgres/migrations/20260824180000_func02_almoxarifado_estoque_requisicoes.sql`.
- **Tabelas:** `almoxarifado_material`, `almoxarifado_local`, `almoxarifado_estoque`, `almoxarifado_movimentacao`, `almoxarifado_requisicao`, `almoxarifado_requisicao_item`, `almoxarifado_requisicao_historico`, `almoxarifado_pendencia_patrimonial` e `almoxarifado_auditoria`, todas no schema `sigov`.
- **Permissões:** as doze chaves listadas no manual FUNC02.
- **Fluxos:** catálogo/local, entrada/saída atômica, ledger imutável, requisição e histórico, atendimento atômico, dashboard real, CSV auditado e fila patrimonial única.
- **Código:** contratos em Application, serviço Dapper em Infrastructure, controller REST, controller MVC e oito views Razor responsivas.
- **Artefatos sincronizados:** manifest, migration e `script_completo.sql`, `script_completop.sql`, `database/script_completo.sql`, `database/postgres/script_completo.sql`, `script_completo_dev.sql` e `database/postgres/script_completo_dev.sql`.

## Arquivos alterados

`README.md`, `PLANO_IMPLEMENTACAO.md`, `PLANO_MESTRE_SIGOV_PLUS.md`, `CHANGELOG.md`, contratos/serviço/controllers/views FUNC02, migration, manifest, scripts consolidados, este manual e este fechamento.

## Gates e evidências

Os resultados abaixo devem refletir somente comandos efetivamente executados nesta entrega e são atualizados no fechamento final:

- **PASS:** `python3 -m json.tool database/postgres/migrations/manifest.json`; verificação Python dos SHA-256 de todas as migrations; comparação byte a byte dos consolidados; `git diff --check`.
- **BLOCKED:** `dotnet restore/build sigov.runtime.slnf` (`dotnet` ausente); aplicação/reaplicação `psql -v ON_ERROR_STOP=1` (`psql` e PostgreSQL 16 seguro ausentes); parser/gerador PowerShell (`pwsh` ausente); subida e smoke/screenshot MVC (dependem do runtime e banco ausentes). Nenhum desses gates recebeu PASS por inferência.
- **FAIL:** reservado a falha observada; uma falha deve ser corrigida antes do commit.

## Estado de releases

FUNC02 é trilha funcional paralela. A **RC50.68 continua BLOCKED** pelos gates oficiais de ambiente/CI/runtime/PostgreSQL. A **RC50.69 não foi iniciada nem marcada** e esta entrega não promove nenhuma das duas.
