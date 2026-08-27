# Jurídico360 — EXP06

## Escopo entregue

O Jurídico360 evolui o FUNC17 existente, sem duplicar pessoas, contribuintes, processos digitais, dívida ativa ou documentos. A central mantém dashboard, carteira processual, prazos, audiências, consultivo, acordos, execução fiscal, documentos, precatórios/RPV, publicações, relatórios CSV neutralizados e trilha auditável sob contexto obrigatório de tenant e entidade.

## Persistência e integrações

A migration `20260827120000_exp06_juridico360_integrado.sql` complementa o processo jurídico e cria carteira, histórico de movimentos, execução/CDA, parcelas de acordo, precatório/RPV, risco, tarefas, alertas, modelos, petições e publicações. As FKs reutilizam `pessoa`, `tributario_divida_ativa`, `processo_digital` (referência no processo), `documento_gerado`, advogado, processo, prazo e acordo oficiais. Valores, estados, encerramentos e vínculos possuem checks; a CDA ativa tem proteção contra ajuizamento duplicado.

## Segurança e operação

As consultas Dapper são parametrizadas por tenant/entidade; nomes de tabelas vêm apenas de allowlist. Escritas, exclusões e exportações registram histórico e auditoria. CSV neutraliza fórmulas. Processos sigilosos e dados sensíveis dependem das permissões administrativas específicas e devem permanecer fail-closed. PJe, e-SAJ, Projudi, tribunais, cartórios, Gov.br e pagamentos não são simulados: sem adaptador oficial, a integração permanece não configurada.

## Interface

`/Juridico` e `/Juridico/Dashboard` exibem indicadores persistidos. As áreas de processos, execuções fiscais, prazos/tarefas, audiências, pareceres, documentos/modelos, acordos, precatórios, publicações e relatórios usam MVC/Razor real. O design responsivo está isolado em `juridico360.css`, com hero sóbrio, cards, navegação móvel, tabelas e estados vazios.
