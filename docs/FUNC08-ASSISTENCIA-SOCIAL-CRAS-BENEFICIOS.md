# FUNC08 — Assistência Social, CRAS/CREAS e Benefícios

## Entrega funcional

O módulo Social usa PostgreSQL como fonte exclusiva, Dapper/Npgsql na infraestrutura e contexto obrigatório de tenant, entidade, usuário e módulo contratado. Abrange famílias e composição, pessoas, domicílios, unidades CRAS/CREAS/Centro POP/acolhimento/gestão, técnicos, prontuário sigiloso, atendimentos, benefícios eventuais, visitas, encaminhamentos, PAIF/PAEFI e SCFV.

## Segurança e LGPD

As permissões `SOCIAL_*` são persistidas e concedidas automaticamente somente ao perfil sistêmico SUPERADMIN. O serviço permanece fail-closed. Consultas a família/pessoa/prontuário e exportações devem gerar auditoria; o banco mantém trilha específica minimizada em `social_auditoria_acesso`. Não há seed de beneficiário nem dado pessoal. CPF/NIS não é exposto por exportação irrestrita.

## Rotas

As telas autenticadas estão sob `/AssistenciaSocial`: Dashboard, Pessoas, Famílias, Domicílios, Unidades, Técnicos, Prontuários, Atendimentos, Benefícios, Visitas, Encaminhamentos, Acompanhamentos, Scfv e Relatórios. O alias `/Social` permanece compatível.

## Banco e relatórios

A migration `20260825020000_func08_assistencia_social_cras_beneficios.sql` é aditiva e idempotente, preserva as tabelas sociais legadas e acrescenta constraints, índices, RBAC, prontuário, domicílio, técnicos, fluxo de solicitação de benefício, ações familiares e SCFV. Relatórios CSV são produzidos pela API a partir do tenant corrente; ausência de schema/conexão é erro explícito, nunca fallback.

## Operação

Aplique com `psql -v ON_ERROR_STOP=1`. Configure somente `ConnectionStrings__DefaultConnection`. Não foi promovida release e InovaGED/Protocolo não integra esta entrega.
