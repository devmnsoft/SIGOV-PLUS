# CORR13 — fechamento técnico do Obras360

## Resultado

- Read models Dapper compatíveis com materialização por propriedades e aliases SQL revisados.
- Autorização fail-closed por recurso nas rotas genéricas de criar, editar, detalhar e salvar.
- Status permitidos definidos por fluxo; rejeição/cancelamento exige justificativa e cronograma, diário e medição exigem data/competência.
- Seleção de obra exclusivamente pelo cadastro oficial; entrada técnica de JSON retirada da interface.
- Validação Razor completa no cadastro de obra e preservação de opções em POST inválido.
- CSV com os filtros de busca, status e período, contexto tenant/entidade, auditoria e proteção contra CSV injection.
- Migration corretiva idempotente e sem `DROP`, manifest e baselines sincronizados.

## Telas revisadas

Dashboard, cadastro, listagem, criação/edição/detalhe genérico, homologação de medição e relatórios, cobrindo as rotas de cronogramas, diários, medições, aditivos, reajustes, reequilíbrios, ocorrências, não conformidades, ordens, evidências e transparência.

## Comandos e bloqueios

- `python3 -m json.tool database/postgres/migrations/manifest.json` — aprovado.
- `sha256sum database/postgres/migrations/20260826170000_corr13_obras360_validacoes.sql` — aprovado.
- buscas `rg` para IDs, antiforgery, validações e marcadores artificiais — aprovadas após revisão.
- BLOCKED: comando `dotnet build` não executado porque o executável `dotnet` não está instalado no ambiente.
- BLOCKED: comando `psql` não executado porque o executável `psql` não está instalado no ambiente e não há conexão PostgreSQL configurada.
- BLOCKED: smoke básico das rotas MVC do Obras360 não executado porque o runtime `dotnet` não está instalado no ambiente.
