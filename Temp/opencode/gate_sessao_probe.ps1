[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'jornada_lib.ps1')
$script:JDbMode='docker'
[void](JDb ("select u.id, u.tenant_id, u.entidade_id, u.exercicio_id, u.login, u.ativo, u.bloqueado, u.is_deleted from sigov.usuario u where u.login in ('admin','superadmin','compras.demo.analista','compras.demo.gestor') order by u.id;"))
[void](JDb ("select md5('sigov:usuario:101'::text)::uuid as sub101, md5('sigov:usuario:102'::text)::uuid as sub102, md5('sigov:usuario:1'::text)::uuid as sub1;"))
[void](JDb ("select m.core_tenant_id, m.enterprise_tenant_id::text, m.ativo from sigov.enterprise_tenant_mapping m where m.ativo order by m.core_tenant_id;"))
[void](JDb ("select count(*) as sessoes_ativas from sigov.identidade_sessao s where s.encerrada_at is null and s.expira_at > now() and coalesce(s.is_deleted,false)=false;"))
