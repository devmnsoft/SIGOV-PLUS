# Testes manuais Pós-RC 05

Use este roteiro em homologação com Docker, banco limpo e seed demo aplicado.

1. Subir Docker com `docker compose up -d`.
2. Aplicar seed demo com `scripts/apply-demo-seed.ps1` ou via `psql`.
3. Fazer login com usuário administrador demo.
4. Abrir Dashboard e conferir KPIs reais.
5. Abrir Minha Central e conferir tarefas/notificações reais.
6. Criar protocolo pela Web.
7. Tramitar protocolo.
8. Anexar documento no GED.
9. Abrir detalhe do protocolo.
10. Conferir timeline.
11. Conferir tarefa criada.
12. Marcar notificação como lida.
13. Buscar protocolo na Busca Global.
14. Exportar relatório CSV.
15. Criar API key.
16. Testar API v1 com key e tenant.
17. Criar webhook.
18. Gerar evento outbox.
19. Conferir entrega ou falha registrada.
20. Validar documento público.
21. Vincular evidência na POC.
22. Gerar relatório de POC quando disponível.
23. Conferir permissões com usuário comum.
24. Conferir que CPF/CNPJ/e-mail/telefone aparecem mascarados onde aplicável.
25. Conferir que storage path, secrets, API keys e webhook secrets não aparecem em telas, CSVs, logs ou respostas públicas.

## Critério de aceite manual

O teste é aceito apenas quando cada evidência contém data/hora, usuário, tenant, tela/endpoint, resultado esperado, resultado observado e pendência honesta quando houver limitação.
