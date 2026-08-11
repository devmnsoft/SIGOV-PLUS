# Execução local do SIGOV+

## Pré-requisitos

- .NET SDK definido pelo repositório;
- PowerShell 7 (`pwsh`);
- PostgreSQL e cliente `psql` 16 ou superior;
- PostgreSQL local acessível em `localhost:5432`.

## Provisionamento em um comando

Na raiz do repositório, execute:

```powershell
./scripts/setup-dev.ps1
```

O script é **exclusivo de Development**. Ele cria o banco `sigov` quando ausente, aplica
o manifesto de migrations, executa o bootstrap/seed local idempotente, provisiona o
usuário de runtime e valida a instalação. A aplicação nunca cria banco automaticamente
em Production.

Se a senha administrativa local do PostgreSQL não for `123456`, informe-a sem gravá-la
no repositório:

```powershell
$env:PGPASSWORD = '<senha-local-do-postgres>'
./scripts/setup-dev.ps1
```

O setup sincroniza `.env.local` (arquivo ignorado pelo Git) e o usuário Development:

- login: `admin`
- senha: `SigovDevLocal!2026`

Essa credencial é recriada com hash PBKDF2 real e existe somente no bootstrap local;
não é fallback de autenticação e não deve ser usada em Production.

## Inicialização

Em terminais separados:

```powershell
dotnet run --project src/Sigov.Api/Sigov.Api.csproj
dotnet run --project src/Sigov.Web/Sigov.Web.csproj
```

URLs locais:

- Web: <https://localhost:7000/Auth/Login>
- Central: <https://localhost:7000/MinhaCentral>
- Swagger da **API**: <https://localhost:7001/swagger>
- OpenAPI JSON: <https://localhost:7001/swagger/v1/swagger.json>

O projeto `Sigov.Web` não hospeda Swagger. Na página de login, Development mostra um
atalho explícito para o Swagger de `Sigov.Api`.

## Fallback sem PowerShell ou psql

Quando `pwsh` ou `psql` não estiverem disponíveis, o próprio Web usa sua connection
string efetiva e os serviços reais de hash e autenticação. Inicie o Web, abra
<https://localhost:7000/Dev/Auth>, clique em **Resetar admin local** e confirme que o
teste retorna `OK`. Depois entre em <https://localhost:7000/Auth/Login> com `admin` /
`SigovDevLocal!2026` e valide a abertura de `/MinhaCentral`.

`/Dev/Auth` retorna 404 fora de Development. Em acesso que não seja loopback, configure
`Sigov:DevBootstrapToken` ou `SIGOV_DEV_BOOTSTRAP_TOKEN` e envie o valor em
`X-Sigov-Dev-Token`. O diagnóstico e o relatório em
`artifacts/local-setup/dev-auth-report.json` nunca contêm senha, hash completo ou
connection string integral.

## Diagnóstico de banco

O erro PostgreSQL `3D000` significa que o banco indicado por
`ConnectionStrings:DefaultConnection` ainda não existe. Execute `setup-dev.ps1`; não
trate essa condição como credencial de login inválida. O Web apresenta uma mensagem
operacional sem stack trace e registra o código de correlação no log. O valor integral
da connection string e suas senhas nunca devem ser registrados.

Para uma inspeção somente leitura adicional:

```powershell
./scripts/diagnose-sigov-database.ps1 -Database sigov -User sigov
```
