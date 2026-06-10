# Planos SaaS SIGOV

## Planos disponíveis

- **STARTER**: plano inicial para pequenos órgãos e implantação piloto, com dashboard, segurança, auditoria, protocolo e GED.
- **GOV_BASIC**: plano para secretarias e órgãos com gestão administrativa, incluindo contratos e RH.
- **GOV_PLUS**: plano completo para prefeituras e estruturas multiáreas, incluindo Tributário, Jurídico, Saúde, Educação, Agro, Saneamento, Social e Integrações.
- **ENTERPRISE**: plano corporativo com módulos ilimitados, white label e domínio customizado.

## Limites

Os limites são parametrizados em `sigov.saas_plano` e detalhados em `sigov.saas_plano_limite`.

- Usuários ativos por tenant.
- Tenants por contratação.
- Armazenamento em MB.
- Permissão de white label.
- Permissão de domínio customizado.
- Limites específicos por módulo quando aplicável.

## Módulos por plano

A tabela `sigov.saas_plano_modulo` define os módulos inclusos por plano. Alterações feitas pela API `/api/saas/planos/{id}/modulos` preservam histórico comercial e permitem bloquear módulos fora do plano sem apagar dados existentes.

## Upgrade

Upgrade troca o plano da assinatura ativa, registra `ASSINATURA_UPGRADE` em `sigov.saas_assinatura_historico` e ativa os módulos inclusos no novo plano.

## Downgrade

Downgrade troca o plano, registra `ASSINATURA_DOWNGRADE` e não remove dados já existentes. Novas ações fora dos limites do plano devem ser bloqueadas ou exibidas como pendência operacional.

## Cancelamento

Cancelamento altera a assinatura para `CANCELADA`, mantém dados do tenant e registra `ASSINATURA_CANCELADA`. A política de bloqueio de login ou modo somente leitura deve ser configurada em evolução posterior.

## Histórico comercial

Eventos comerciais são gravados em `sigov.saas_evento_comercial`; mudanças de assinatura são detalhadas em `sigov.saas_assinatura_historico` com usuário e correlation ID.
