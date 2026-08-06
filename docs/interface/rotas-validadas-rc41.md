# Rotas validadas — RC41

Execute `pwsh scripts/check-web-routes.ps1` para auditoria estática ou informe `-BaseUrl http://localhost:5000` para também verificar respostas HTTP. Redirecionamentos para autenticação e respostas 401/403 são válidos; 404 não é.

O relatório versionável da execução é gravado em `artifacts/web/routes-report.json`. A matriz cobre Dashboard/Minha Central, Protocolo, GED, Tarefas, Notificações, Busca, Segurança, implantação, Auditoria e LGPD.

## Contrato de fallback

Rotas operacionais consultam o schema real por tenant. Quando uma tabela opcional ainda não existe, a interface informa a indisponibilidade sem inventar registros ou confirmar persistência. Ações sensíveis continuam protegidas no controller e auditadas com correlation ID.

