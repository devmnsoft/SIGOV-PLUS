select 'tenants', count(*) from sigov.tenant
union all select 'usuarios', count(*) from sigov.usuario
union all select 'protocolos', count(*) from sigov.protocolo
union all select 'documentos', count(*) from sigov.documento
union all select 'tarefas', count(*) from sigov.tarefa
union all select 'notificacoes', count(*) from sigov.notificacao
union all select 'api_keys', count(*) from sigov.api_key
union all select 'api_key_escopos', count(*) from sigov.api_key_escopo
union all select 'webhooks', count(*) from sigov.webhook_configuracao
union all select 'outbox', count(*) from sigov.outbox_evento
order by 1;
