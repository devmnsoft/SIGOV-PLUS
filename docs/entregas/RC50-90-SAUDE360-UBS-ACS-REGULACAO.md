# Entrega RC50.90 — Saúde360

- Schema aditivo e idempotente para 29 agregados de saúde, com contexto multi-tenant/multi-esfera, checks e índices.
- Permissões granulares de dashboard, unidade, paciente, dado sensível, ACS, agenda, regulação, farmácia, vigilância, relatórios e portal.
- Rotas MVC para unidades, pacientes, ACS, filas, regulação, farmácia e vigilância; início seguro do Portal do Cidadão.
- Dashboard institucional sem limitação municipal e orientação de uso.

## Limites
Integrações oficiais externas dependem de adaptador contratado e configurado. Ausência de schema, permissão ou configuração deve produzir erro explícito, nunca fallback.
