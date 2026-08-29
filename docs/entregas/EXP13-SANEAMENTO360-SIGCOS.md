# EXP13 — Saneamento360 / SIGCOS

Entrega evolutiva sobre o saneamento já publicado, sem duplicar pessoa, consumidor, ligação, hidrômetro, fatura, pagamento, OS, equipe, endereço ou protocolo.

## Entregue

- Endereços MVC canônicos para os fluxos comercial, leitura/faturamento, arrecadação, campo, GIS e laboratório.
- Complemento PostgreSQL idempotente e não destrutivo para aferições/substituições, revisões, faixas, cobranças, materiais e alertas.
- 22 permissões granulares persistidas, sem concessão automática.
- Workspace premium responsivo com indicadores, filtros, badges, estados vazios, auditoria e exportação.
- Documentação de integração real, LGPD, PWA e limite GIS.

## Operação

Configure exclusivamente `ConnectionStrings__DefaultConnection`. Banco e autorização são fontes de autoridade. Integrações indisponíveis falham explicitamente e não são simuladas.
