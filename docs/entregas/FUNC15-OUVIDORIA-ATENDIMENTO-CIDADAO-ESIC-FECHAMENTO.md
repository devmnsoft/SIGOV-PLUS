# Fechamento corretivo FUNC15

## Auditoria e correções

A revisão cobriu controller, views, contratos, repositório Dapper, registro de DI, menu, migration, manifest e scripts consolidados. Foram corrigidos: expressão Razor inválida no `action` do formulário; geração de URLs de Novo/Salvar/Cancelar; chamada do contexto autenticado pela Carta pública; exposição potencial da exportação de Ouvidoria; aceitação de JSON vazio; auditoria excessiva do conteúdo do formulário; e ausência do filtro `ativo` nas listagens.

A Carta pública resolve tenant e entidade por parâmetros positivos ou, em sessão autenticada, por claims. Contexto ausente produz 404 controlado. A consulta retorna exclusivamente registros ativos, com status `ATIVO` e não excluídos.

## Rotas revisadas

`/AtendimentoCidadao`, `/Dashboard`, `/Cidadaos`, `/CartaServicos`, `/Demandas`, `/Ouvidoria`, `/Esic`, `/Encaminhamentos`, `/Agendas`, `/Agendamentos`, `/Sla`, `/Satisfacao`, `/BaseConhecimento`, `/Relatorios` e `/Auditoria`. Os slugs internos são validados por lista permitida antes de qualquer nome de tabela ser usado.

## Segurança

As ações operacionais continuam fail-closed por policy. Relatórios exigem `ATENDIMENTO_RELATORIO_EXPORT`; auditoria exige `ATENDIMENTO_AUDITORIA_VIEW`; CSV de Ouvidoria também exige `OUVIDORIA_SIGILO_VIEW`. Exclusão permanece lógica, justificada, contextual e transacional. Auditoria de escrita registra somente código e status, não o corpo potencialmente pessoal/sigiloso.

## Banco e scripts

A migration publicada `20260825090000_func15_ouvidoria_atendimento_esic.sql` não foi alterada. O manifest JSON e a presença da migration nos quatro scripts consolidados foram verificados. A execução real via `psql -v ON_ERROR_STOP=1` ficou **BLOCKED**: cliente/servidor PostgreSQL não estão disponíveis no ambiente.

## Build e smoke test

`dotnet build sigov.sln --no-restore` ficou **BLOCKED** porque o executável `dotnet` não existe no ambiente. Pelo mesmo motivo, compilação Razor e smoke test HTTP autenticado (criações, exportação, auditoria e Forbid) não puderam ser executados e não são declarados como PASS.

## Limites

Nenhum arquivo de InovaGED, GED ou Protocolo foi alterado. FUNC16 não foi iniciado.
