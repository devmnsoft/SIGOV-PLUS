# Auditoria .NET e pacotes Pós-RC 15

Gerado em 2026-07-14 para a consolidação Pós-RC 15 do SIGOV PLUS.

## Configuração global auditada

| Item | Valor atual | Evidência | Avaliação | Ação recomendada |
|---|---|---|---|---|
| TargetFramework | `net6.0` | `Directory.Build.props` | Funcional para a base atual, porém fora de suporte Microsoft. | Manter temporariamente apenas se produção exigir; planejar migração controlada para .NET 8 LTS ou .NET 10 LTS. |
| LangVersion | `10.0` | `Directory.Build.props` | Compatível com `net6.0`. | Ao migrar para .NET 8/10, avaliar C# 12/14 em branch própria. |
| Nullable | `enable` | `Directory.Build.props` | Bom para evitar nulos em runtime. | Não desabilitar. |
| TreatWarningsAsErrors | `true` | `Directory.Build.props` | Adequado para release candidate; qualquer warning quebra build. | Manter. |
| AnalysisLevel | `latest` | `Directory.Build.props` | Pode ativar novas regras conforme SDK instalado. | Validar em SDK fixo no CI para evitar variação inesperada. |
| EnforceCodeStyleInBuild | `true` | `Directory.Build.props` | Exige consistência de estilo no build. | Manter. |
| Central Package Management | `true` | `Directory.Packages.props` | Evita versões divergentes por projeto. | Manter versões centralizadas. |

## Matriz de pacotes

| Pacote | Versão atual | Projeto que usa | Compatível com net6? | Compatível com net8? | Compatível com net10? | Ação recomendada |
|---|---:|---|---|---|---|---|
| Dapper | 2.1.35 | Infrastructure, Worker | Sim | Sim | Provável, validar em branch .NET 10 | Manter; baixo risco. |
| Npgsql | 6.0.11 | Infrastructure | Sim | Parcial/legado | Não recomendado sem upgrade | Não atualizar às cegas; para .NET 8/10 validar breaking changes de Npgsql 7/8/10, timestamp, multiplexing e pool. |
| Serilog.AspNetCore | 6.1.0 | Api, Web, Infrastructure, Worker | Sim | Legado | Não recomendado sem upgrade | Manter no .NET 6; ao migrar, alinhar major com ASP.NET Core e validar configuração. |
| Serilog.Settings.Configuration | 3.4.0 | Api, Worker | Sim | Legado | Não recomendado sem upgrade | Atualizar somente com teste de appsettings e sinks. |
| Serilog.Sinks.Console | 4.1.0 | Api, Web, Infrastructure, Worker | Sim | Sim | Provável | Manter ou atualizar em branch dedicada. |
| Serilog.Sinks.File | 5.0.0 | Api, Web, Infrastructure, Worker | Sim | Sim | Provável | Manter; validar retenção e paths sem vazamento. |
| FluentValidation | 11.9.2 | Application | Sim | Sim | Provável | Manter; qualquer upgrade deve validar regras existentes. |
| FluentValidation.DependencyInjectionExtensions | 11.9.2 | Api | Sim | Sim | Provável | Manter pareado com FluentValidation. |
| Microsoft.AspNetCore.Authentication.JwtBearer | 6.0.36 | Api | Sim | Não ideal | Não | Ao migrar framework, atualizar para 8.x ou 10.x junto do TFM. |
| Swashbuckle.AspNetCore | 6.5.0 | Api | Sim | Sim | Provável com validação | Validar geração OpenAPI após migração. |
| xunit | 2.9.2 | Unit, Integration, ApiTests | Sim | Sim | Provável | Manter. |
| xunit.runner.visualstudio | 2.8.2 | Unit, Integration, ApiTests | Sim | Sim | Provável | Manter. |
| Microsoft.NET.Test.Sdk | 17.11.1 | Unit, Integration, ApiTests | Sim | Sim | Provável | Manter; validar SDK instalado. |
| FluentAssertions | 6.12.1 | Unit, Integration, ApiTests | Sim | Sim | Provável | Manter; upgrades podem exigir ajustes de assert. |
| Testcontainers.PostgreSql | 3.10.0 | IntegrationTests | Sim | Sim | Provável | Não atualizar sem validar Docker e lifecycle dos containers. |
| Microsoft.AspNetCore.Mvc.Testing | 6.0.36 | ApiTests | Sim | Não ideal | Não | Atualizar junto do TFM ASP.NET Core em branch própria. |
| Microsoft.Extensions.Options | 6.0.0 | Application | Sim | Legado | Não recomendado | Atualizar junto do TFM. |
| Microsoft.Extensions.Hosting.Abstractions | 6.0.0 | Application | Sim | Legado | Não recomendado | Atualizar junto do TFM. |
| Microsoft.Extensions.Logging.Abstractions | 6.0.0 | Application | Sim | Legado | Não recomendado | Atualizar junto do TFM. |

## Comandos de auditoria solicitados

Neste workspace, `dotnet` não está instalado; portanto `dotnet list package`, `--outdated`, `--vulnerable`, `restore`, `build` e `test` foram bloqueados por limitação de ambiente. Nenhum pacote foi atualizado sem validação. A decisão segura para Pós-RC 15 é documentar riscos e executar a matriz no CI ou em ambiente com SDK .NET 6/8 instalado antes de homologar runtime.

## Riscos de migração

- .NET 8 exige atualizar os pacotes Microsoft.AspNetCore/Microsoft.Extensions/Mvc.Testing de 6.x para 8.x e trocar imagens Docker SDK/runtime.
- .NET 10 exige salto maior de compilador, analyzers e bibliotecas; deve ser tratado como migração de plataforma, não hotfix.
- Npgsql deve ser validado com migrations, Dapper, transações e tipos PostgreSQL antes de qualquer upgrade major.
- Serilog deve ser validado com `appsettings`, sinks e mascaramento LGPD antes de qualquer upgrade major.
- FluentValidation deve preservar mensagens, severidade e comportamento dos validators existentes.
- Testcontainers deve ser validado com Docker real e PostgreSQL saudável.
