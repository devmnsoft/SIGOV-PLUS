# Bloqueios de produção

**P0 (bloqueia):** migration/build/API/Web/login falha; senha dev ou connection string exposta; Swagger público; PII crítica sem máscara; exportação sem auditoria; rota crítica 500/menu 404; worker crítico falha sem log. Todo P0 deve estar encerrado com evidência.

**P1 (aprovação excepcional):** dashboard vazio sem explicação, relatório essencial ausente, enforcement parcial, observabilidade ou rollback incompletos. Exige responsável, prazo e mitigação.

**P2 (backlog):** polimento visual, performance secundária e integrações preparatórias. Não mascarar P0/P1 como P2.
