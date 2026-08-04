# Instalação one-shot do banco SIGOV+

> RC39 acrescenta diagnóstico final automático, reparo seguro e validações de runtime. Nenhuma senha é persistida pelos scripts; use `PGPASSWORD` e `SIGOV_BOOTSTRAP_ADMIN_PASSWORD` somente no ambiente do processo.

O instalador canônico do banco é `scripts/install-sigov-database.ps1`.

Ele executa, em uma única chamada:

1. valida o PostgreSQL 16 ou superior;
2. cria o banco físico quando ele não existe;
3. aplica as migrations na ordem do `manifest.json`;
4. repara estruturas legadas antes das migrations críticas;
5. cria ou atualiza tenant, entidade e exercício;
6. cria o administrador inicial com hash PBKDF2 compatível com a aplicação;
7. cria perfil, grupo, permissões e escopos do administrador;
8. habilita módulos e plano completo do tenant inicial;
9. insere configurações e parâmetros padrão;
10. executa novamente para validar idempotência;
11. valida se o banco ficou pronto para o runtime.

## Pré-requisitos

- PostgreSQL 16 ou superior;
- cliente `psql` disponível no `PATH`;
- PowerShell 7 recomendado;
- usuário PostgreSQL com permissão para criar banco, quando o banco ainda não existir.

## Instalação local

No PowerShell, a partir da raiz do repositório:

```powershell
$env:PGPASSWORD = 'SENHA_DO_POSTGRES'

./scripts/install-sigov-database.ps1 `
  -HostName localhost `
  -Port 5432 `
  -Database sigov `
  -User postgres
```

Quando nenhuma senha administrativa é informada, o instalador gera uma senha temporária forte e a mostra uma única vez no terminal.

Login inicial padrão:

```text
admin
```

E-mail inicial padrão:

```text
admin@sigov.local
```

A senha não possui valor fixo no repositório e é armazenada somente como hash `SIGOV_PBKDF2_V1`.

## Informar a senha administrativa

```powershell
$env:PGPASSWORD = 'SENHA_DO_POSTGRES'
$env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD = 'SENHA_TEMPORARIA_FORTE'

./scripts/install-sigov-database.ps1 `
  -Database sigov `
  -User postgres
```

A senha deve possuir no mínimo 12 caracteres e será marcada para alteração no primeiro acesso.

## Instalação personalizada

```powershell
$env:PGPASSWORD = 'SENHA_DO_POSTGRES'

./scripts/install-sigov-database.ps1 `
  -HostName localhost `
  -Port 5432 `
  -Database sigov_municipio `
  -MaintenanceDatabase postgres `
  -User postgres `
  -TenantName 'Município Exemplo' `
  -TenantSlug 'municipio-exemplo' `
  -TenantDocument '00000000000191' `
  -EntityName 'Prefeitura Municipal de Exemplo' `
  -EntityCnpj '00000000000000' `
  -ExerciseYear 2026 `
  -Environment PRODUCTION `
  -AdminLogin admin `
  -AdminEmail 'administrador@exemplo.gov.br' `
  -AdminName 'Administrador Geral'
```

## Recriar um banco local

A opção abaixo exclui o banco informado e cria outro do zero. Não deve ser usada em produção sem backup validado.

```powershell
./scripts/install-sigov-database.ps1 `
  -Database sigov `
  -User postgres `
  -Recreate
```

## Redefinir a senha do administrador

Uma nova execução preserva a credencial administrativa válida já existente. A redefinição precisa ser explícita:

```powershell
$env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD = 'NOVA_SENHA_TEMPORARIA_FORTE'

./scripts/install-sigov-database.ps1 `
  -Database sigov `
  -User postgres `
  -ResetAdminPassword
```

Hashes legados incompatíveis, como registros `DEV_ONLY:`, são substituídos automaticamente por um hash aceito pela aplicação.

## Banco parcialmente criado

O instalador pode ser executado sobre um banco que parou no meio da instalação. O preflight idempotente repara as colunas esperadas pelas migrations críticas antes de continuar.

Para manter o banco recém-criado mesmo quando ocorrer uma falha, exclusivamente para diagnóstico:

```powershell
./scripts/install-sigov-database.ps1 `
  -Database sigov `
  -User postgres `
  -KeepFailedDatabase
```

Por padrão, um banco novo e incompleto é removido automaticamente quando a instalação falha.

## Resultado

Ao concluir, o instalador cria `artifacts/database/install-result.json` sem senha e sem hash, contendo:

- banco;
- tenant;
- login administrativo;
- exercício;
- indicação de provisionamento da credencial;
- confirmação da validação de idempotência;
- data da conclusão.

## Observação sobre execução SQL isolada

Um arquivo SQL comum, aberto em uma conexão já estabelecida, não consegue de maneira portátil criar outro banco e continuar automaticamente a execução dentro dele. Por isso, o instalador PowerShell é o ponto único de entrada: ele cria o banco, reconecta, aplica o schema e executa o bootstrap completo.
## Fluxo recomendado (PostgreSQL 16+)

```powershell
$env:PGPASSWORD = Read-Host 'Senha PostgreSQL' -MaskInput
$env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD = Read-Host 'Senha inicial (12+ caracteres)' -MaskInput
./scripts/install-sigov-database.ps1 -Database sigov -RunDiagnosticsBefore -RunDiagnosticsAfter
./scripts/validate-sigov-runtime.ps1 -Database sigov
```

O instalador cria o banco físico quando necessário, aplica o manifest, executa o bootstrap e repete a aplicação para provar idempotência. A segunda execução reaproveita tenant e administrador e preserva todo hash `SIGOV_PBKDF2_V1` válido. Use `-ResetAdminPassword` somente para uma troca intencional.

`script_completop.sql` é o baseline SQL autocontido e idempotente do **schema**. `install-sigov-database.ps1` é o orquestrador operacional: cria o banco, aplica o manifest completo, injeta o bootstrap sem armazenar segredo, diagnostica e produz `artifacts/database/install-result.json`.

Mensagens `already exists, skipping` são esperadas em reexecuções idempotentes. Por padrão elas ficam fora do resumo; `-VerboseSql` mostra a saída SQL completa. Elas só indicam problema quando acompanhadas de `ERROR`, falha de pós-condição ou exit code não zero.

Consulte [Diagnóstico e reparo](diagnostico-e-reparo.md) para bancos parciais, `-WhatIf`, reset administrativo e interpretação dos relatórios.
