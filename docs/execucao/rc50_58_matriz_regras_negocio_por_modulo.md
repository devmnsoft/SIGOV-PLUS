# RC50.58 — matriz de regras de negócio por módulo

| Domínio | Regras obrigatórias para homologação | Controle observado / pendência |
|---|---|---|
| Tributário/Financeiro | documento ou motivo; exercício/tributo; DAM válido; baixa única; cancelamento/estorno justificado; dívida vencida | serviços avançados validam status/justificativa e persistem auditoria; provar baixa concorrente |
| Educação | matrícula anual única; frequência ativa; transferência encerra anterior; estoque não negativo; versão de cardápio; exemplar disponível | núcleos Bloco3/Avançado existem; provar constraints e máscaras |
| Saúde | CPF/CNS mascarado; dado clínico por grant; visita completa; vacina/medicamento válidos; estoque não negativo; lote idempotente | núcleo e avançado existem; e-SUS/SISAB preparatório |
| Saneamento | hidrômetro ativo único; leitura regressiva justificada; baixa única; corte elegível; religação após corte; OS com desfecho | repositório avançado exige justificativas; provar integração financeira |
| Processos/GED | número único; destino na tramitação; sigilo; imutabilidade pós-assinatura; encerramento/cancelamento justificado | cancelamento canônico agora exige justificativa, rejeita estado terminal e audita antes/depois |
| Compras/Contratos/Patrimônio | aprovação autorizada; fornecedor/vigência/fiscal; saldo não negativo; tombamento único; baixa motivada; origem rastreável | núcleos persistentes; obrigação financeira ainda preparatória |
| RH/Folha | vínculo/lotação; períodos sem sobreposição; folha fechada imutável; reabertura motivada; PII protegida | núcleo persistente; provar fechamento e ponte financeira |
| Frotas/Obras | ativo/data/quantidade; responsável; diário só em obra ativa; paralisação motivada; retomada válida | controllers/services presentes; provar transições e auditoria |
| LGPD/Auditoria | protocolo/responsável/status; incidente com severidade e sem hard delete; exportação/falha auditada; logs sem segredo/PII | serviços transversais presentes; revisar cada exportador legado |

## Perfis

SUPERADMIN possui bypass explícito. ADMIN_TENANT fica limitado ao tenant e módulos habilitados. GESTOR/COORDENADOR/GESTOR_MODULO dependem da área concedida. OPERACIONAL cria/edita sem aprovação crítica. FINANCEIRO requer grants distintos para baixa, cancelamento e estorno. AUDITOR e LEITURA não alteram operação. ATENDIMENTO limita-se a protocolo/ouvidoria/e-SIC. CIDADAO acessa somente recursos próprios no portal externo. O backend, e não a visibilidade do menu, é a autoridade.
