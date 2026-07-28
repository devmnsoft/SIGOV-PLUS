# Alterações incompatíveis .NET 10

| Área | Arquivo | Comportamento anterior | Comportamento .NET 10 | Correção | Regressão |
|---|---|---|---|---|---|
| Build | `Directory.Build.props` | net6/C#10/analyzers flutuantes | net10/C#14/analyzers 10 | propriedades determinísticas centralizadas | build Release obrigatório |
| Hosting | Dockerfiles de API/Web | runtime ASP.NET 6 | runtime ASP.NET 10 | imagens oficiais 10.0 | smoke de health obrigatório |
| Hosting | Dockerfile do Worker | imagem ASP.NET desnecessária | runtime .NET 10 | imagem runtime mínima | ciclo de vida do Worker obrigatório |
| Packages | `Directory.Packages.props` | contratos Microsoft e Npgsql 6 | contratos 10 | referências centrais 10.0.0 | testes unitários/API/integração obrigatórios |
