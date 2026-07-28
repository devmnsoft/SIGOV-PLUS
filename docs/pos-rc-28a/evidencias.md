# Evidências

| Verificação | Resultado |
|---|---|
| SHA local inicial | `51d5475037cf01150010e141fda9833ec268d01a` |
| Fetch do origin | bloqueado por HTTP 403 |
| SDK local | executável `dotnet` ausente |
| Contrato de configuração | migrado para net10.0/C#14/analyzers 10 |
| Dockerfiles | bases oficiais 10.0 |
| Workflows | setup-dotnet 10.0.x |

Somente resultados observados são registrados. Gates que dependem de restore, PostgreSQL, Windows, Docker ou GitHub Actions não são declarados verdes.
