# Contrato de execução — SIGOV PLUS

Programa recebido em 2026-09-08. Esta é a memória operacional vigente; prevalece sobre roadmaps históricos conflitantes. RC própria: P0-GOV-20260908, fora da RC50.68A.

## Sequência obrigatória

P0 (migrations, build, Swagger, login e isolamento) → P1 (SaaS) → P2 (Indústria por fluxo vertical) → P3 → P4 → GED por último. Falha ou BLOCKED em P0 impede liberar funcionalidades das fases posteriores. Aprovação de escopo permite trabalhar no item; aprovação do gate exige evidência runtime.

## Contrato técnico

- .NET 10/C# 14 conforme global.json; PostgreSQL 16 no gate e 16+ no produto; Dapper, sem EF. MVC/Razor, API e JavaScript normal; preservar Domain/Application/Infrastructure/Api/Web/Worker.
- Preservar tenant_id, entidade_id, exercicio_id, usuário e escopo quando aplicáveis. Backend é a autoridade. Queries parametrizadas; nunca concatenar entrada em SQL nem retornar dados de outro tenant.
- Operações compostas usam transação única. Concorrência, idempotência e auditoria são parte do fluxo.
- Migrations publicadas são imutáveis; correções forward-only, idempotentes, compatíveis com vazio e legado. Não renumerar versões aplicadas, editar histórico/checksum de schema_migrations ou adicionar órfãs automaticamente.
- Todos os aplicadores recusam versão/checksum desconhecido no ledger. `knownChecksums` exige pós-condição específica e validação do estado final; compatibilidade pós-migrations só executa quando esta passagem realmente aplicou migration.
- Sincronizar manifest, baseline, runner, apply_all_required_migrations e todos os scripts completos. Compatibilidade exige justificativa e pós-condições específicas.
- Novas PKs bigint identity; preservar UUID legado. Exclusões administrativas usam soft delete, motivo e auditoria; nunca apagar fisicamente registros financeiros, fiscais, contratuais, produtivos, de segurança ou auditoria.
- Banco governa perfis, permissões, parâmetros, catálogo comercial e contratos. Não duplicar serviços/interfaces/modelos canônicos.
- Não criar mock, fake, dados inventados ou fallback silencioso produtivo. Indisponibilidade bloqueia com mensagem clara. Não esconder erro com null-forgiving, ToString, vazio, ID fixo ou catch genérico; registrar contexto seguro e tomar ação coerente.
- Revisar chamadores antes de mudar contrato público. Não expor senha, token, documento completo ou segredo em logs; usar ConnectionStrings__DefaultConnection e segredos do ambiente.
- Formulários mutáveis: validação backend, antiforgery quando aplicável, confirmação e feedback. Seleção por nome/código, busca, dropdown ou autocomplete; IDs técnicos não são a experiência principal.
- Multi-esfera municipal/estadual/federal: esfera_governo, tipo_entidade, órgão superior, unidades gestora/executora, hierarquia e jurisdição quando aplicáveis; regras parametrizadas.
- Preservar alterações do usuário; sem reset --hard, checkout destrutivo ou force push. Branch por fase, commits pequenos, build/testes antes de commit; impedimento real registrado.
- Não criar classes de teste; ampliar as existentes. Validar sintaxe de YAML/JSON/shell/PowerShell alterados e instalar no CI exatamente o SDK de global.json.

## Maturidade

INEXISTENTE, ESTRUTURA, PARCIAL, FUNCIONAL, HOMOLOGADO ou PRODUCAO.
FUNCIONAL exige conjuntamente: schema/migration, contratos tipados, Dapper, regras, API/controller, UI real, validação, autorização, tenant, auditoria, transação/concorrência, estados vazio/erro, testes críticos, menu por contratação e evidência runtime. Controller/view/documento isolado não comprova funcionalidade.

## Continuidade

Ao encerrar um fluxo, atualizar STATUS_REAL_MODULOS, BACKLOG_EXECUTAVEL e ULTIMA_EXECUCAO com evidências e próximo item exato. Não iniciar GED enquanto fases anteriores estiverem pendentes sem decisão formal. Não declarar todo o programa concluído ao entregar uma fase.
