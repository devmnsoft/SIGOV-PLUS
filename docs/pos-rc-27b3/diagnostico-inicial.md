# Diagnóstico inicial — Pós-RC 27B.3

- SHA-base confirmado: `59d7720fd6c2d2ff9c27e8bad6394f559372b81b`.
- Execução de origem solicitada: GitHub Actions `30276103871`, número `302`.
- Distribuição informada na demanda: 15 falhas de host/PostgreSQL, 12 de caminhos, 4 de DI e 23 de contratos/asserts (54 no total).
- O checkout não contém os artifacts TRX, o GitHub CLI/autenticação, o SDK .NET, PowerShell ou Docker. A consulta anônima ao run retornou HTTP 401. Por integridade de evidência, nenhum FQN foi inventado.
- A inspeção local confirmou ambiente `Development` no job `build-test`, caminhos ascendentes frágeis e índices históricos que pressupunham colunas modernas.

Os resultados deste diretório distinguem verificações executadas de verificações bloqueadas pelo ambiente. CI, PostgreSQL, runtime, imagens e navegador não são declarados verdes sem execução real.
