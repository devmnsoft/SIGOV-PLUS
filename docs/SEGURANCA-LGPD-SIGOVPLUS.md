# Segurança e LGPD do SIGOV-PLUS

A autorização é fail-closed e as permissões persistidas no PostgreSQL são a fonte de autoridade. A RC50.81 registra no catálogo canônico os acessos de homologação, SaaS, segurança, operação, design system e relatórios.

## Controles obrigatórios

- isolamento por tenant em consulta, alteração e exportação;
- antiforgery em POST MVC, queries Dapper parametrizadas e confirmação justificada para ações críticas;
- CPF, CNS, telefone, e-mail e endereço mascarados quando a finalidade não autorizar exposição;
- CSV protegido contra fórmulas e exportação auditada;
- mensagens ao usuário sem stack trace, SQL, payload ou segredo; correlação técnica apenas por identificador;
- logs sem corpo sensível e acesso técnico restrito;
- headers CSP, HSTS em produção, `nosniff`, `DENY`, referrer e permissions policy mantidos no pipeline Web.

`sigov.operacao_evento_auditoria` armazena metadados mínimos de operação, resultado, duração e correlação; não deve receber payload nem dado pessoal.
