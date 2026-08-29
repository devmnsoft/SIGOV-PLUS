# ACS360 — FUNC11

## Operação

ACS360 cobre áreas, microáreas, histórico de vínculo do agente, rotas, domicílios, famílias, moradores, indivíduos, condições, visitas, atividades, alimentação, ocorrências, focos de risco, produtividade e sincronização. Todas as seleções nas telas usam cadastros consultados por API; nenhum operador digita ID técnico.

Visitas exigem ACS, microárea, data, turno e desfecho (`REALIZADA`, `RECUSADA` ou `AUSENTE`). GPS respeita latitude -90..90 e longitude -180..180. Visita futura e atuação fora da microárea possuem campos explícitos de justificativa auditável. Um índice parcial impede dois vínculos ativos exclusivos na mesma microárea, preservando o histórico de substituições.

## Offline e antifraude

Lotes e itens possuem chave de idempotência, estados operacionais, erros sanitizados e conflitos resolvidos administrativamente. A sincronização usa HTTP pela rede. Este repositório não contém aplicativo Android nativo: não foi simulada captura de câmera nem bloqueio de galeria. Evidências aceitam somente metadados, hash e referência GED real.

## Rotas

As rotas `/Saude/ACS/*` oferecem território, áreas, microáreas, agentes, domicílios, indivíduos, visitas, atividades, marcadores, ocorrências, focos, sincronização, conflitos e staging e-SUS. Estados vazios e indisponibilidade da fonte oficial são explícitos.
