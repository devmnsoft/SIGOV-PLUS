# FUNC16 — Habitação e Regularização Fundiária

## Escopo

O módulo operacionaliza famílias e sua composição, domicílios, programas, inscrições, critérios, pontuação e classificação, visitas sociais/técnicas, núcleos, lotes, unidades, regularização, beneficiários, termos, histórico e auditoria. PostgreSQL é a autoridade; documentos são somente referências textuais/metadados, sem upload ou integração GED/Protocolo.

## Segurança e contexto

Todas as consultas exigem `tenant_id`, `entidade_id` e `is_deleted=false`. As rotas usam as permissões `HABITACAO_*` persistidas pela migration. Escritas críticas usam transação com auditoria, usuário e correlation id; exclusões são lógicas e justificadas.

## Rotas

`/Habitacao/Dashboard`, `/Familias`, `/Domicilios`, `/Programas`, `/Inscricoes`, `/Classificacao`, `/Visitas`, `/Regularizacao`, `/Nucleos`, `/Lotes`, `/Unidades`, `/Beneficiarios`, `/Relatorios` e `/Auditoria`, todas sob `/Habitacao`.

## Regras

Inscrição ativa é única por família/programa. Estados terminais e exceções exigem justificativa. Pontuação não pode ser negativa. Coordenadas e datas são validadas no banco. Reserva/entrega exige inscrição ou beneficiário. CSV aplica neutralização de `=`, `+`, `-` e `@`.

## Operação

Aplicar `database/postgres/migrations/20260825100000_func16_habitacao_regularizacao_fundiaria.sql` com `psql -v ON_ERROR_STOP=1`. A conexão segue `ConnectionStrings__DefaultConnection`.
