# Diagnóstico inicial — Pós-RC 28B

- SHA esperado: `1a419c382667b18f6caa6af9ba8160a543637468`.
- SHA local inicial: `1a419c382667b18f6caa6af9ba8160a543637468`.
- `origin/main`: não verificável neste ambiente. O repositório chegou sem remote e o acesso HTTPS ao GitHub foi recusado pelo proxy (`CONNECT tunnel failed, response 403`).
- Workflow solicitado: ID `30400626285`, execução `318`.
- Estado dos artefatos: indisponíveis neste ambiente; nenhum diagnóstico foi inventado.
- SDK local: indisponível (`dotnet: command not found`).

O trabalho partiu do SHA esperado porque ele coincide exatamente com o `HEAD` fornecido. A validação contra a main remota e a importação dos artefatos continuam bloqueadas e devem ser repetidas em um runner com acesso ao GitHub.
