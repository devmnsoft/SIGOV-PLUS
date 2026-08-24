# Fechamento FUNC06 — Saúde, Atenção Básica e Regulação

Entrega funcional paralela com migration PostgreSQL corretiva/idempotente, Dapper/Npgsql já integrado, RBAC persistente, auditoria antes/depois, proteção LGPD, APIs e telas MVC/Razor operacionais. O fechamento cobre agenda, risco, prontuário SOAP/retificação, procedimentos, carteira vacinal, dispensação e fila regulatória.

## Validações e limites do ambiente

Foram executados `git diff --check`, parse JSON, checksum integral do manifest e verificações estáticas de rotas/artefatos. `dotnet restore` e `dotnet build sigov.runtime.slnf` somente recebem PASS se o SDK 10 estiver disponível. A aplicação/reaplicação `psql -v ON_ERROR_STOP=1` e o smoke MVC autenticado ficam **BLOCKED** quando não houver PostgreSQL 16 seguro ou aplicação executável; nenhum PASS é inferido.

Scripts consolidados e manifest permanecem sincronizados. Não há seed, segredo ou dado pessoal real. RC50.68 continua **BLOCKED** pelos gates oficiais; RC50.69 não foi iniciada; Protocolo/GED/InovaGED continua adiado para a etapa final.
