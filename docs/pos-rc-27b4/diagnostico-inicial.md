# Diagnóstico inicial — Pós-RC 27B.4

- Base confirmada: `fba0f5e9b20907bdfb21d2a595e5940a46fafa22`.
- Referência solicitada: workflow run `30307186680`, run number `306`.
- O contêiner não dispõe de `gh`, `dotnet` ou `pwsh`; portanto resultados e artefatos remotos não puderam ser consultados localmente.
- O diagnóstico do código confirmou referências a `WebApplicationFactory<Program>` em 11 testes da API e um teste Web, enquanto `Sigov.Testing` referenciava os dois hosts.
- Portão 0 permanece aberto até execução integral no GitHub Actions; os portões 1 e 2 não foram iniciados.
