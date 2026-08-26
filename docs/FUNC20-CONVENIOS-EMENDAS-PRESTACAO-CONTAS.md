# FUNC20 — Convênios, Emendas e Prestação de Contas

## Arquitetura e segurança

O FUNC20 usa MVC/Razor, contratos na camada Application e repositório Dapper/Npgsql na Infrastructure. Todas as consultas e mutações exigem `tenant_id` e `entity_id`, usam parâmetros e uma whitelist fechada de recursos, colunas, relacionamentos e conversões. A ausência do contexto autenticado falha explicitamente.

As relações são selecionadas por opções obtidas do banco no mesmo contexto. IDs são apenas valores internos dos `select`; nunca são apresentados como campos para digitação. As permissões `CONVENIO_*` separam dashboard, consulta, gestão, exportação e auditoria.

## Fluxos e regras

O módulo entrega dashboard, órgãos concedentes, programas, emendas, instrumentos, projetos, metas, etapas, contas, repasses, despesas, contrapartidas, acompanhamentos, prestações, diligências, documentos, auditoria e CSV. Formulários fazem validação no servidor, exibem resumo e mensagens por campo, usam antiforgery e recarregam as opções após erro.

O valor global aceita diferença máxima de **R$ 0,01** em relação a repasse + contrapartida. Datas, valores, percentuais e fechamentos são validados tanto na aplicação quanto no PostgreSQL. Aprovação ou rejeição de prestação é bloqueada enquanto houver diligência aberta/em resposta. Criação, alteração, exclusão, envio, aprovação, rejeição e resposta de diligência deixam trilha transacional em `sigov.convenio_auditoria`.

## Banco e operação

A migration funcional publicada `20260826100000_func20_convenios_emendas_prestacao_contas.sql` não foi alterada. A corretiva idempotente de baseline `20260826110000_corr20_convenios_integridade_indices.sql` fecha domínios de status, coerência de vínculos/fechamentos e índices dos principais filtros. Migration, manifesto e os quatro scripts consolidados permanecem sincronizados.

O dashboard e os relatórios consultam dados reais. A exportação CSV preserva os filtros, limita o volume operacional e neutraliza células iniciadas por `=`, `+`, `-`, `@`, tabulação ou retorno para impedir formula injection.
