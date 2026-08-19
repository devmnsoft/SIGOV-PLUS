# Deploy Windows/IIS

Instale Hosting Bundle .NET compatível e ferramentas PostgreSQL. Crie app pools sem código gerenciado, identidades dedicadas e sites API/Web com bindings HTTPS; ACL mínima para publicação, logs e storage. Defina variáveis/secrets no mecanismo protegido do servidor, nunca no repositório ou `web.config` distribuído.

Faça backup e migration via `psql -v ON_ERROR_STOP=1`; publique diretório versionado, altere o apontamento IIS e recicle. Verifique health/readiness, login, ProjectStatus, logs e só então inicie o Worker como Windows Service. Rollback retorna ao diretório anterior; incompatibilidade de banco requer restore validado/cutover. Bloqueie Swagger externo, force HTTPS e troque a senha bootstrap.
