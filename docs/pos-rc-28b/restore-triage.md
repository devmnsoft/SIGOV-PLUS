# Triagem de restore

O erro original da execução 318 não pôde ser recuperado sem acesso aos logs do GitHub. O CI agora executa restore em modo locked e preserva `restore.log`, `restore.binlog` e `restore-summary.json`, inclusive quando o restore falha.

No snapshot inicial não há `packages.lock.json`; portanto, o novo gate deve permanecer vermelho até os locks serem gerados por `dotnet restore sigov.sln --force-evaluate` e validados com `--locked-mode`.
