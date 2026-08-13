# Roadmap da infraestrutura de testes

## Decisão desta rodada

Os testes ficam para uma etapa posterior. Tanto `sigov.sln` quanto o build oficial em `sigov.runtime.slnf` contêm apenas projetos de produção. Os testes unitários e os utilitários compartilhados foram preservados em `sigov.tests.sln`, para execução opt-in em pipeline separado.

O projeto `Sigov.IntegrationTests` continua fisicamente no repositório, mas não integra nenhuma solução. Por padrão, ele não restaura pacotes de teste, não referencia projetos e não compila suas classes. Uma futura execução explícita pode habilitá-lo com:

```bash
dotnet test tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj -p:EnableSigovIntegrationTests=true
```

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
3. gerar um novo `packages.lock.json` da integração somente com `-p:EnableSigovIntegrationTests=true`, pois o lock contaminado anterior foi removido;
4. executar a auditoria de pacotes na solução/pipeline de testes e tratar todos os achados;
5. executar os testes unitários e de integração em pipeline separado do build runtime.
