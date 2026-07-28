# Diagnóstico inicial — Pós-RC 28C

## Base Git

- Data da verificação: 2026-07-28 (UTC).
- SHA base esperado: `cc49bdccf7cf83ea7e684b70848fbf07c27e4d64`.
- SHA real disponível no checkout: `cc49bdccf7cf83ea7e684b70848fbf07c27e4d64`.
- Branch recebida no ambiente: `work`.
- `git fetch origin`: **não executado com sucesso**, pois o checkout não possui um remote `origin` configurado (`fatal: 'origin' does not appear to be a git repository`).
- `origin/main`: indisponível para resolução no ambiente.

O SHA local coincide com o SHA base esperado, mas não foi possível comprovar se a
branch `main` remota avançou. Portanto, nenhuma alteração de schema ou runtime foi
iniciada.

## Portão 0 — restore, build e testes

O comando `dotnet restore sigov.sln` não pôde ser executado porque o SDK `dotnet`
não está instalado no ambiente (`dotnet: command not found`). Como o restore não
passou, os comandos de build e teste encadeados não foram iniciados.

Também foi tentada a obtenção do instalador oficial do SDK 10.0.100, definido em
`global.json`, mas a requisição de rede retornou HTTP 403.

## Decisão de segurança

O Portão 0 permanece vermelho. Em respeito à ordem obrigatória dos portões, este
registro de diagnóstico é a única mudança realizada: não houve modificação em SQL,
migrations, código C#, configurações, Docker, versão ou baseline canônico.

Para prosseguir, o ambiente deve fornecer:

1. o remote `origin` apontando para `devmnsoft/SIGOV-PLUS`;
2. o SDK .NET 10.0.100 (ou versão compatível conforme `global.json`);
3. acesso às dependências necessárias ao restore;
4. PostgreSQL 16, Docker e PowerShell para os portões posteriores.
