# Smoke tests HTTP e diagnóstico runtime

Documento operacional para registrar a validação das rotas principais no Docker local.

| URL | Status code | Resultado | Erro encontrado | Correção aplicada | Pendência |
| --- | ---: | --- | --- | --- | --- |
| http://localhost:8080/Auth/Login | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Executar após `docker compose up -d --build` |
| http://localhost:8080/Dashboard | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Dashboard usa fallback honesto | Validar HTTP |
| http://localhost:8080/MinhaCentral | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Manual | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Poc | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Saas/Tenants | Não executado | Pendente | Consultas antigas assumiam colunas opcionais | Consulta ajustada para schema-safe | Validar HTTP |
| http://localhost:8080/Saas/Modulos | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Saas/Parametros | Não executado | Pendente | Parâmetros dependem do schema real | Listagem já mascara sensíveis e não simula persistência | Validar HTTP |
| http://localhost:8080/Seguranca/Usuarios | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Seguranca/Perfis | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Seguranca/Permissoes | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Relatorios | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Auditoria/Trilhas | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Lgpd/Dashboard | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
| http://localhost:8080/Operacao/Health | Não executado | Pendente | Health anterior declarava componentes online sem prova | Health passa a usar probes reais ou “Não monitorado” | Validar HTTP |
| http://localhost:5001/api/health/live | Não executado | Pendente | SDK/ambiente runtime indisponível nesta sessão | Roteiro documentado | Validar HTTP |
