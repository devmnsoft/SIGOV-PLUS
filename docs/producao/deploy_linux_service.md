# Deploy Linux (systemd)

Instale o ASP.NET Core Runtime compatível, cliente PostgreSQL e usuário sem shell. Publique Release em diretório versionado, mantenha storage/log fora dele e injete variáveis por `EnvironmentFile` protegido (`0600`). Use units distintas para API, Web e Worker, `WorkingDirectory`, `ExecStart=/usr/bin/dotnet ...dll`, restart com atraso e dependência de rede. Proxy Nginx/Apache termina TLS e encaminha protocolo/host; exponha apenas HTTPS.

Antes do switch do symlink: backup, migration com `psql -v ON_ERROR_STOP=1`, smoke interno. Reinicie API/Web, valide readiness, então Worker. Rollback aponta ao release anterior; banco só volta por restore/cutover aprovado. Swagger permanece desligado e a senha inicial é trocada antes da abertura.
