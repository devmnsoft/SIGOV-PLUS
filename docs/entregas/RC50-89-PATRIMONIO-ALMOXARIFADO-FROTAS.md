# Entrega RC50.89 — Patrimônio, Almoxarifado, Frotas e Manutenção

## Entregue

- migration corretiva idempotente para depreciação, imóveis, transferências, multas, documentos, OS, agenda preventiva, integração contábil e auditoria;
- contexto multi-esfera e índices operacionais;
- 27 permissões canônicas, concedidas de forma idempotente ao Super Administrador sistêmico;
- rotas MVC/Razor canônicas para dashboards, CRUDs e relatórios, reaproveitando os serviços Dapper oficiais;
- documentação operacional e regras de LGPD, auditoria e integração real.

## Critérios

Valores monetários e quantitativos usam `numeric`/`decimal`; checks impedem valores, quantidades e períodos inválidos. O modelo não cria autoridade hardcoded e não expõe documento sensível. Todos os comandos de escrita existentes permanecem protegidos por antiforgery e autorização contextual.

BASE LOCAL utilizada porque origin/main não estava disponível.
