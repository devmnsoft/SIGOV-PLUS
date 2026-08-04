# Diagnóstico, reparo e validação operacional

## Diagnosticar

```powershell
./scripts/diagnose-sigov-database.ps1 -HostName localhost -Port 5432 -Database sigov -User postgres
```

O comando verifica conexão e PostgreSQL 16+, existência do banco/schema, extensões referenciadas, tabelas/colunas/índices essenciais, manifest e checksums, duplicidades, hash administrativo, vínculos, parâmetros e feature flags. Gera JSON, Markdown e log em `artifacts/database/diagnostics/`.

* `0`: saudável;
* `1`: há `WARNING` ou `ERROR`, mas o ambiente é analisável;
* `2`: `CRITICAL`, conexão indisponível ou banco irrecuperável automaticamente.

## Reparar com segurança

Primeiro simule:

```powershell
./scripts/repair-sigov-database.ps1 -Database sigov -WhatIf
./scripts/repair-sigov-database.ps1 -Database sigov -Apply -Confirm:$false
```

O reparador somente adiciona extensões/compatibilidade/índices, normaliza campos de permissão e completa parâmetros, flags e vínculos existentes. Não remove linhas nem executa `DROP TABLE`. `-Force` registra a intenção de uma operação mais forte, mas não elimina a confirmação nem habilita ações destrutivas.

Para corrigir **somente hashes administrativos incompatíveis**, forneça a nova senha pelo ambiente e confirme explicitamente:

```powershell
$env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD = Read-Host 'Nova senha (12+ caracteres)' -MaskInput
./scripts/repair-sigov-database.ps1 -Database sigov -Apply -ResetAdminPassword -Confirm:$false
```

A senha e seu hash nunca aparecem nos relatórios. O usuário deverá alterá-la no próximo acesso. Administradores com hash atual são preservados.

## Recuperar uma execução parcial

1. Execute o diagnóstico e arquive os três relatórios.
2. Rode o reparo em `-WhatIf`.
3. Se as ações forem todas aditivas, rode `-Apply`.
4. Reexecute o instalador sem `-Recreate`, de preferência com `-KeepFailedDatabase` durante a investigação.
5. Execute a validação runtime:

```powershell
./scripts/validate-sigov-runtime.ps1 -Database sigov
```

Os seis validadores são somente leitura, idempotentes e falham objetivamente com `raise exception`. O resultado fica em `artifacts/database/validation/`. Problemas de checksum de migration não são alterados pelo reparador: compare o manifest, identifique a origem e corrija o histórico de forma auditada.

## Instalação assistida

`-RunDiagnosticsBefore`, `-RepairBeforeInstall`, `-RepairAfterInstall`, `-Quiet`, `-VerboseSql` e `-FailOnWarnings` permitem adequar a operação. O diagnóstico pós-bootstrap é habilitado por padrão. Para reexecução segura, não use `-Recreate`; esse parâmetro é deliberadamente destrutivo para o banco inteiro e deve ficar restrito a ambientes descartáveis.
