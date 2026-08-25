# Validação de telas, formulários e design

## Escopo e método
Auditoria estática em 25/08/2026 nas views funcionais: Patrimônio (7), Almoxarifado (9), Compras (14), Frotas (18), Educação (92), Saúde (34), Saneamento (35), Assistência Social (`Views/Social`), Tributário (45), Financeiro/SIAFIC (27), Agro (107), RH/Folha (77), Obras (7), Meio Ambiente (5), Atendimento ao Cidadão (5), Habitação (5) e Jurídico (5). Foram inspecionados formulários, Tag Helpers, navegação, feedback, estados vazios e responsividade. Form Tag Helpers POST geram antiforgery automaticamente; no Jurídico o token também ficou explícito.

## Problemas e correções
| Tela | Problema | Correção |
|---|---|---|
| Jurídico / navegação | URLs literais; Relatórios misturado à lista genérica | `asp-controller`, `asp-action` e `asp-route-*`; action própria de Relatórios |
| Jurídico / relatórios | download interpolado em `href` | Tag Helper para `Csv` |
| Jurídico / formulários | CSRF somente implícito | token explícito em Salvar e Excluir; actions já usam `ValidateAntiForgeryToken` |
| Jurídico / lista | perfil somente leitura via botões de escrita | controles agora dependem de `*_MANAGE` |
| Jurídico / contexto | tenant/entidade ausente podia produzir 500 | `Forbid`, sem fallback |
| Jurídico / auditoria | JSON funcional duplicado na trilha | histórico conserva estado; auditoria guarda metadados |
| Jurídico / listagem | busca/status, vazio, tabela responsiva, paginação e justificativa | preservados; submit de exclusão explicitado |
| Demais módulos | nenhum bug específico comprovado na inspeção | sem alteração ampla, evitando regressão visual/regra de negócio |

## Formulários e rotas
- Salvar e Excluir no Jurídico têm CSRF no Razor e validação no controller.
- Descrição/status são obrigatórios; JSON e justificativa são validados; falha de salvar retorna o modelo e `ModelState`.
- Exclusão exige justificativa no cliente e servidor.
- As 16 rotas jurídicas documentadas foram confirmadas estaticamente. Auditoria é somente leitura.
- Relatórios exigem `JURIDICO_RELATORIO_EXPORT`; Auditoria exige `JURIDICO_AUDITORIA_VIEW`.

## Banco e consolidados
- Migration FUNC17 presente no manifest com SHA-256 correspondente.
- Bloco FUNC17 presente em `script_completo.sql`, `script_completo_dev.sql`, `database/script_completo.sql` e `script_completop.sql`.
- Permissões de relatório e auditoria persistidas pela migration.

## BLOCKED objetivos
| Comando/atividade | Motivo | Impacto |
|---|---|---|
| `dotnet restore` / `dotnet build sigov.sln --no-restore` | `dotnet` ausente | build e compilação Razor não executados |
| `psql -v ON_ERROR_STOP=1 -f database/postgres/migrations/20260825110000_func17_procuradoria_juridica_contencioso.sql` | `psql` e PostgreSQL ausentes | migration não executada em banco real |
| smoke HTTP das 16 rotas | aplicação não pode iniciar sem SDK/banco | confirmação somente estática |
| navegador/screenshot | aplicação não executável | responsividade inspecionada apenas no Razor/Bootstrap |

FUNC18 não foi iniciado. InovaGED, GED e Protocolo não tiveram regra de negócio alterada.
