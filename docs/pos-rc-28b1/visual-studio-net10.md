# Visual Studio e .NET 10 — Pós-RC 28B.1

## Pré-requisitos no Windows

- Visual Studio com suporte ao .NET 10 e workload **ASP.NET e desenvolvimento Web**;
- SDK .NET 10.0.100 ou uma feature band compatível permitida por `global.json`;
- PostgreSQL 16 acessível pela connection string do ambiente;
- PowerShell 7 para os scripts de automação que usam `pwsh`.

Docker não é requisito de restore, compilação, testes ou execução local. Ele é
somente uma opção de provisionamento descrita em outros documentos.

## Roteiro de validação

1. Abra `sigov.sln` no Visual Studio e confirme que o SDK selecionado é .NET 10.
2. Execute **Restore NuGet Packages** e confirme que o restore locked não altera
   arquivos `packages.lock.json`.
3. Compile a solution em **Debug** e **Release**, com zero warnings e zero erros.
4. Selecione separadamente os perfis de `Sigov.Api`, `Sigov.Web` e
   `Sigov.Worker`; configure a conexão PostgreSQL e execute cada host.
5. Abra o Test Explorer, descubra toda a suíte e execute todos os testes sem
   filtros. Confirme que não há falhas nem testes obrigatórios ignorados.

Na linha de comando equivalente, use `dotnet restore sigov.sln --locked-mode`,
`dotnet build` e `dotnet test`. API, Web e Worker podem ser iniciados com
`dotnet run --project`, sem Docker.
