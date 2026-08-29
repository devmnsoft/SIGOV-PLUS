# Saúde360 — FUNC11

## Escopo entregue

O Saúde360 consolida a entrada autenticada em `/Saude` e `/Saude/Dashboard`, navegação para unidades, profissionais, ACS360, relatórios e quatro vigilâncias. O dashboard lê a API oficial no contexto corrente; ausência de schema, contexto ou permissão é exibida como falha, nunca substituída por números fictícios.

A expansão cria eventos, alertas, notificações e ações de vigilância epidemiológica, sanitária, ambiental e do trabalhador. Severidade, prazo, responsável, status e encerramento têm restrições no PostgreSQL. Ocorrências ACS podem ser vinculadas a eventos, sem criar protocolo ou encaminhamento artificial.

## Segurança e LGPD

Dados territoriais, clínicos, CPF/CNS e localização são sensíveis. A aplicação minimiza listas, exige autenticação, mantém `tenant_id` e `entidade_id`, e separa permissões de consulta, gestão e exportação. Criação, alteração, sincronização e exportação usam a auditoria já existente no módulo. CSV é produzido pelos exportadores transversais, que neutralizam células iniciadas por `=`, `+`, `-` ou `@`.

## Integrações e limites

Prontuário, pessoa, profissional e unidade canônicos são reutilizados. GED só é referenciado por identificador existente; não há upload falso. Não foi implementado envio ao Ministério da Saúde: o banco oferece staging, validação e inconsistências. **BLOCKED:** layout oficial e credenciais de transmissão e-SUS/SISAB não estão versionados no repositório.
