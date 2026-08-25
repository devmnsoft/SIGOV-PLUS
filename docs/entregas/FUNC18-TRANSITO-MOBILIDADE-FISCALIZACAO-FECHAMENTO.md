# Fechamento técnico CORR18 — FUNC18

## Correções realizadas

- Persistência Dapper protegida por whitelist explícita de campos para cada recurso, sem coluna arbitrária oriunda do formulário.
- Isolamento por tenant/entidade na listagem, opções, gravação, exclusão, dashboard e CSV; vínculos são validados no mesmo contexto.
- Seletores reais e identificáveis para todos os relacionamentos, com CPF mascarado onde aplicável e nenhum input manual de ID.
- Antiforgery nos POSTs, resumo de validação, preservação de campos e recarga das listas após erro.
- Validações server-side para autos, notificações, recursos, julgamentos, ocorrências, sinalização, intervenções, rotas, autorizações, vistorias e credenciais.
- Filtros reais por busca, status e período; CSV limitado às oito famílias autorizadas e protegido contra CSV injection.
- Dashboard permanece inteiramente baseado em consultas PostgreSQL do contexto ativo; navegação responsiva reorganizada em Cadastros, Fiscalização e Transporte urbano.
- Migration corretiva idempotente, manifesto e scripts consolidados sincronizados, sem alterar a migration FUNC18 publicada.

## Validação e bloqueios reais

- `python3 -m json.tool database/postgres/migrations/manifest.json`: aprovado.
- checksum SHA-256 da migration corretiva comparado ao manifesto: aprovado.
- inspeção com `rg` de formulários relacionais e antiforgery: aprovada.
- `dotnet restore sigov.sln` e `dotnet build sigov.sln --no-restore`: **BLOCKED** — `dotnet: command not found`.
- aplicação da migration em PostgreSQL local: **BLOCKED** — `psql: command not found`.
- smoke manual em navegador: **BLOCKED** — a aplicação não pode ser iniciada sem o runtime .NET e sem PostgreSQL.
