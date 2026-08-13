# Roadmap da infraestrutura de testes

## Decisão desta rodada

Os testes unitários e de integração ficam para uma etapa posterior. O build principal do produto usa `sigov.runtime.slnf`, que contém apenas projetos de produção, enquanto `sigov.sln` preserva os projetos de teste para manutenção em fluxo separado.

Não há hoje um projeto `src/Sigov.Shared/Sigov.Shared.csproj`; por isso, o filtro inclui os seis projetos de produção existentes: API, Web, Worker, Application, Domain e Infrastructure.

## Pendências conhecidas

- dependência transitiva SSH.NET trazida por `Testcontainers.PostgreSql`, incluindo versão vulnerável;
- falha de validação de hash NU1403 observada no pacote SSH.NET;
- falha de restore NU1102 relacionada a `BouncyCastle.Cryptography`;
- atualização da dependência `Testcontainers.PostgreSql` e de seu grafo transitivo.

Pins diretos de SSH.NET e BouncyCastle.Cryptography não devem ser usados para contornar o grafo transitivo quando o código de teste não consome esses pacotes diretamente. Vulnerabilidades também não devem ser suprimidas com `NoWarn` nem com a desativação global de `TreatWarningsAsErrors`.

## Próxima etapa de testes

1. atualizar `Testcontainers.PostgreSql` para uma versão cujo grafo transitivo esteja disponível e sem vulnerabilidades conhecidas;
2. limpar o cache NuGet no agente dedicado;
3. restaurar e atualizar os arquivos `packages.lock.json` dos testes;
4. executar `dotnet list sigov.sln package --vulnerable --include-transitive` e tratar todos os achados;
5. executar os testes unitários e de integração em pipeline separado do build runtime.
