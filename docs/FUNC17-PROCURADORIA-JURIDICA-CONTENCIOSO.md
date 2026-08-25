# FUNC17 — Procuradoria Jurídica, Contencioso e Dívida Ativa Judicial

## Escopo
Módulo operacional multi-tenant para advogados, partes, processos, movimentações, prazos, intimações, audiências, consultas, pareceres, acordos, obrigações, dívida ativa judicial e custas.

## Arquitetura e segurança
PostgreSQL é a fonte de autoridade. A aplicação usa Dapper/Npgsql, parâmetros SQL, whitelist de recursos, filtros obrigatórios de `tenant_id`, `entidade_id`, `ativo` e `is_deleted`, transações para histórico/auditoria e RBAC `JURIDICO_*`. JSON é validado antes do cast para `jsonb` e vazio vira `{}`. Documentos e integrações são apenas referências textuais/metadados; não há upload ou alteração em GED, InovaGED, Protocolo ou Tributário.

## Operação
Rotas sob `/Juridico` oferecem dashboard calculado no banco, filtros, paginação, formulários antiforgery, exclusão lógica justificada e CSV neutralizado contra fórmulas (`=`, `+`, `-`, `@`). Estados críticos e emissão de parecer têm validações na aplicação e no banco.

## Instalação
Aplique `database/postgres/migrations/20260825110000_func17_procuradoria_juridica_contencioso.sql` com `psql -v ON_ERROR_STOP=1`. Configure exclusivamente `ConnectionStrings__DefaultConnection` no ambiente.

## Fechamento de segurança e rotas
Relatórios exigem `JURIDICO_RELATORIO_EXPORT`; auditoria exige `JURIDICO_AUDITORIA_VIEW`, é somente leitura e guarda metadados sem duplicar o JSON funcional. Controles de alteração só aparecem após avaliação da permissão `*_MANAGE`. Ausência do contexto oficial resulta em `Forbid`, sem substituto.

Rotas confirmadas: `/Juridico`, `/Juridico/Dashboard`, `/Juridico/Advogados`, `/Juridico/Partes`, `/Juridico/Processos`, `/Juridico/Movimentacoes`, `/Juridico/Prazos`, `/Juridico/Intimacoes`, `/Juridico/Audiencias`, `/Juridico/Pareceres`, `/Juridico/Consultas`, `/Juridico/Acordos`, `/Juridico/DividaAtiva`, `/Juridico/Custas`, `/Juridico/Relatorios` e `/Juridico/Auditoria`.
