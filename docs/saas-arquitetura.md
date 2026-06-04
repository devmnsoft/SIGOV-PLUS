# Arquitetura SaaS do sigov

O sigov é uma plataforma SaaS de gestão pública municipal. O tenant representa o cliente contratante; entidade representa a prefeitura, câmara, fundo, secretaria ou autarquia operada dentro do tenant; exercício representa o ano fiscal de trabalho.

A estratégia atual usa banco compartilhado, schema único `sigov`, coluna `tenant_id` em tabelas operacionais, filtros obrigatórios em repositories Dapper e preparação de Row-Level Security por `sigov.current_tenant_id()`.

## Provisionamento

O endpoint `POST /api/saas/admin/tenants/provisionar` cria tenant, domínio, assinatura, módulos, entidade, exercício, pessoa administradora, usuário administrador por convite e evento operacional, sempre em transação.
