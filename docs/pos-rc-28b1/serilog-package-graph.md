# Grafo de pacotes Serilog — Pós-RC 28B.1

## Escopo e método

Este inventário registra o grafo esperado pelo Central Package Management após o
alinhamento com `Serilog.AspNetCore` 10.0.0. O arquivo JSON ao lado é o artefato
legível por máquina. O job `package-security` regenera o grafo efetivamente
resolvido e a auditoria de vulnerabilidades em CI.

| Projeto | Diretos | Transitivos relevantes | Versão resolvida / mínima | Origem | Conflito e correção |
|---|---|---|---|---|---|
| Sigov.Api | AspNetCore, Settings.Configuration, Sinks.Console, Sinks.File | Serilog core | 10.0.0 / 10.0.0; 6.1.1 / 6.1.1; 7.0.0 / 7.0.0 | host API e AspNetCore | As versões centrais 3.4.0, 4.1.0 e 5.0.0 causavam NU1605; foram alinhadas. |
| Sigov.Web | AspNetCore, Settings.Configuration, Sinks.Console, Sinks.File | Serilog core | 10.0.0 / 10.0.0; 6.1.1 / 6.1.1; 7.0.0 / 7.0.0 | host Web e AspNetCore | Mesmo downgrade; versões centrais alinhadas. |
| Sigov.Worker | AspNetCore, Settings.Configuration, Sinks.Console, Sinks.File | Serilog core | 10.0.0 / 10.0.0; 6.1.1 / 6.1.1; 7.0.0 / 7.0.0 | host Worker e AspNetCore | Mesmo downgrade; versões centrais alinhadas. |
| Sigov.Infrastructure | nenhum | nenhum pacote Serilog necessário | n/a | n/a | Referências sem uso a AspNetCore e sinks foram removidas; logging continua nos hosts. |
| Sigov.Testing | nenhum | pacotes dos hosts por ProjectReference | conforme os hosts | Sigov.Api e Sigov.Web | Nenhum PackageReference novo. |
| Sigov.ApiTests | nenhum | pacotes do host API | conforme Sigov.Api | Sigov.Api e Sigov.Testing | Nenhum PackageReference novo. |
| Sigov.IntegrationTests | nenhum | pacotes do host Web | conforme Sigov.Web | Sigov.Web e Sigov.Testing | Nenhum PackageReference novo. |
| Sigov.UnitTests | nenhum | pacotes dos projetos sob teste | conforme os hosts | Sigov.Testing, Web e Worker | Nenhum PackageReference novo. |

Não há `VersionOverride`, versão individual em `PackageReference`, `HintPath` ou
supressão de NU1605. As referências de logging permanecem exclusivamente nos
projetos host que usam diretamente `UseSerilog`, configuração e sinks.

