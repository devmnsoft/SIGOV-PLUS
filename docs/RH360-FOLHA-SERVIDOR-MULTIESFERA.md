# RH360 e Folha multi-esfera — RC50.88

## Contexto institucional

O RH360 usa obrigatoriamente `tenant_id`, entidade, exercício, esfera de governo, órgão e unidades gestora/executora. O mesmo núcleo atende administrações municipais, estaduais e federais sem regras municipais implícitas. A hierarquia, abrangência territorial e jurisdição são persistidas no contexto do registro.

## Servidor e vida funcional

A pessoa canônica é `rh_servidor`; vínculos, matrícula, cargo, função, lotação, movimentações e atos são independentes. CPF não é exposto: a unicidade ativa usa hash por tenant. Matrícula é única na entidade. Frequência, férias, licenças e afastamentos alimentam as pendências anteriores à folha. Dados médicos, previdenciários e remuneratórios exigem permissão específica e auditoria sensível.

## Folha

A competência segue abertura, cálculo, conferência, fechamento e reabertura justificada. Valores usam `numeric(18,2)`/`decimal`; evento guarda natureza e incidências e o cálculo conserva itens e memória. Integração financeira apenas registra a solicitação quando o módulo e a autorização reais existem; não há integração bancária simulada.

## Operação e exportações

Dashboards e listagens consultam a base oficial filtrada pelo contexto. As exportações CSV neutralizam células iniciadas por `=`, `+`, `-` e `@`. Falta de schema, contexto ou adaptador é erro explícito. As permissões `RH_*`, `FOLHA_*` e `PORTAL_SERVIDOR_*` são autoridade do banco.
