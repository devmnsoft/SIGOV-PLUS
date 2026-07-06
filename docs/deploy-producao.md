# Deploy de produção/homologação SIGOV PLUS

1. Provisionar servidor Linux com Docker, Docker Compose, volume para PostgreSQL e storage.
2. Criar `.env.production` a partir de `.env.production.example`.
3. Configurar domínio, HTTPS e reverse proxy.
4. Subir `docker compose up -d --build`.
5. Validar health API/Web, migrations, worker, SMTP, storage, backup e restore.
6. Executar smoke test e registrar evidências.
7. Manter rollback com imagem anterior, backup de banco e storage.

Não publicar com secrets padrão, stacktrace público ou backup não testado.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
