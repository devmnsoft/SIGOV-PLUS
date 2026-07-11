# Evidências Enterprise Pós-RC 10

- `node --check src/Sigov.Web/wwwroot/js/enterprise-crud.js`: executado com sucesso no container.
- `node --check src/Sigov.Web/wwwroot/js/enterprise-form-metadata.js`: executado com sucesso no container.
- `dotnet build sigov.sln --configuration Release`: não executado porque `dotnet` não está instalado no container.
- Evidência runtime Docker/smoke deve ser anexada por CI/CD com ambiente .NET/Docker/PowerShell disponível.
