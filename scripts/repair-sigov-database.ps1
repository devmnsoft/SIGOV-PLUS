[CmdletBinding(SupportsShouldProcess, ConfirmImpact='High')]
param(
    [string]$HostName='localhost', [int]$Port=5432, [string]$Database='sigov',
    [string]$MaintenanceDatabase='postgres', [string]$User='postgres', [string]$PsqlPath='psql',
    [string]$SchemaName='sigov', [string]$OutputDirectory='artifacts/database/repair',
    [switch]$Apply, [switch]$Force, [switch]$ResetAdminPassword,
    [string]$AdminPassword=$env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
function Invoke-Psql([string]$Sql,[string]$File='') {
    $args=@('-X','-q','-v','ON_ERROR_STOP=1','-h',$HostName,'-p',$Port,'-U',$User,'-d',$Database)
    if($File){$args+=@('-f',$File)}else{$args+=@('-c',$Sql)}
    $out=& $PsqlPath @args 2>&1; if($LASTEXITCODE-ne 0){throw ($out-join "`n")}; return ($out-join "`n")
}
function New-PasswordHash([string]$PlainText){
    $salt=[byte[]]::new(24); [Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $pbkdf=[Security.Cryptography.Rfc2898DeriveBytes]::new($PlainText,$salt,210000,[Security.Cryptography.HashAlgorithmName]::SHA256)
    try{return 'SIGOV_PBKDF2_V1$210000${0}${1}' -f [Convert]::ToBase64String($salt),[Convert]::ToBase64String($pbkdf.GetBytes(32))}finally{$pbkdf.Dispose()}
}
$root=Split-Path -Parent $PSScriptRoot; $outDir=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $root $OutputDirectory}; New-Item -ItemType Directory -Force $outDir|Out-Null
if($SchemaName-notmatch'^[A-Za-z_][A-Za-z0-9_]{0,62}$'){throw 'SchemaName inválido.'}
$actions=[Collections.Generic.List[object]]::new(); $status='success'; $mode=if($Apply){'APPLY'}else{'WHAT_IF'}
try{
    if($Apply -and $WhatIfPreference){throw 'Use -Apply ou -WhatIf, não ambos.'}
    if(-not $Apply -and -not $WhatIfPreference){$WhatIfPreference=$true; $mode='WHAT_IF'}
    $workflowCompat=Join-Path $root 'database/postgres/bootstrap/060_pre_rc49_workflow_compatibility.sql'
    $compat=Join-Path $root 'database/postgres/bootstrap/850_post_migration_compatibility.sql'
    $actions.Add([ordered]@{action='workflow-rc49-compatibility';safe=$true;applied=$false;detail='Normalização aditiva do contrato legado de workflow antes da RC49.'})
    if($Apply -and $PSCmdlet.ShouldProcess($Database,'Normalizar contrato legado de workflow')){Invoke-Psql '' $workflowCompat|Out-Null;$actions[$actions.Count-1].applied=$true}
    $actions.Add([ordered]@{action='legacy-compatibility';safe=$true;applied=$false;detail='Colunas e índices idempotentes de compatibilidade.'})
    if($Apply -and $PSCmdlet.ShouldProcess($Database,'Aplicar compatibilidade e índices seguros')){Invoke-Psql '' $compat|Out-Null;$actions[$actions.Count-1].applied=$true}
    $sql=@"
create extension if not exists pgcrypto;
create extension if not exists "uuid-ossp";
update $SchemaName.permissao set recurso=lower(split_part(chave,':',1)), acao=lower(coalesce(nullif(split_part(chave,':',2),''),'acessar')) where recurso is null or acao is null;
insert into $SchemaName.tenant_configuracao(tenant_id,chave,valor,secreto,ativo,is_deleted)
select t.id,v.chave,v.valor,false,true,false from $SchemaName.tenant t cross join (values ('sistema.locale','"pt-BR"'::jsonb),('sistema.timezone','"America/Sao_Paulo"'::jsonb),('sistema.moeda','"BRL"'::jsonb),('sistema.bootstrap_concluido','true'::jsonb))v(chave,valor) where t.ativo and not t.is_deleted on conflict(tenant_id,chave) do nothing;
insert into $SchemaName.tenant_feature_flag(tenant_id,feature_flag_def_id,modulo_codigo,feature_codigo,habilitado,habilitada,valor,parametros_json,ativo,is_deleted)
select t.id,f.id,coalesce(f.modulo,split_part(f.codigo,'.',1)),f.codigo,true,true,'{}','{}',true,false from $SchemaName.tenant t cross join $SchemaName.feature_flag_def f where t.ativo and not t.is_deleted and f.ativo and not f.is_deleted on conflict(tenant_id,feature_flag_def_id) do nothing;
insert into $SchemaName.usuario_grupo(tenant_id,usuario_id,grupo_acesso_id,is_deleted)
select u.tenant_id,u.id,g.id,false from $SchemaName.usuario u join $SchemaName.grupo_acesso g on g.tenant_id=u.tenant_id and g.nome='Administradores' and not g.is_deleted where u.tipo_usuario='ADMINISTRADOR_GERAL' and not u.is_deleted on conflict(usuario_id,grupo_acesso_id) do update set is_deleted=false;
insert into $SchemaName.grupo_perfil(tenant_id,grupo_acesso_id,perfil_acesso_id,is_deleted)
select g.tenant_id,g.id,p.id,false from $SchemaName.grupo_acesso g join $SchemaName.perfil_acesso p on p.tenant_id=g.tenant_id and p.codigo_externo='ADMINISTRADOR_GERAL' and not p.is_deleted where g.nome='Administradores' and not g.is_deleted on conflict(grupo_acesso_id,perfil_acesso_id) do update set is_deleted=false;
insert into $SchemaName.perfil_permissao(tenant_id,perfil_acesso_id,permissao_id)
select p.tenant_id,p.id,x.id from $SchemaName.perfil_acesso p cross join $SchemaName.permissao x where p.codigo_externo='ADMINISTRADOR_GERAL' and not p.is_deleted and x.ativo and not x.is_deleted on conflict(perfil_acesso_id,permissao_id) do nothing;
"@
    $actions.Add([ordered]@{action='safe-runtime-repair';safe=$true;applied=$false;detail='Extensões, normalização, parâmetros, flags e vínculos.'})
    if($Apply -and $PSCmdlet.ShouldProcess($Database,'Aplicar reparos de runtime não destrutivos')){Invoke-Psql $sql|Out-Null;$actions[$actions.Count-1].applied=$true}
    if($ResetAdminPassword){
        if([string]::IsNullOrWhiteSpace($AdminPassword)-or$AdminPassword.Length-lt 12){throw 'ResetAdminPassword exige AdminPassword (ou SIGOV_BOOTSTRAP_ADMIN_PASSWORD) com 12+ caracteres.'}
        $hash=(New-PasswordHash $AdminPassword).Replace("'","''"); $actions.Add([ordered]@{action='reset-admin-password';safe=$true;applied=$false;detail='Redefinir apenas administradores com hash incompatível.'})
        if($Apply -and $PSCmdlet.ShouldProcess($Database,'Redefinir hashes administrativos incompatíveis')){Invoke-Psql "update $SchemaName.usuario set senha_hash='$hash',senha_deve_ser_alterada=true,deve_alterar_senha=true,updated_at=now() where tipo_usuario='ADMINISTRADOR_GERAL' and not is_deleted and coalesce(senha_hash,'') not like 'SIGOV_PBKDF2_V1$%';"|Out-Null;$actions[$actions.Count-1].applied=$true}
    }
}catch{$status='failed';$actions.Add([ordered]@{action='error';safe=$false;applied=$false;detail=$_.Exception.Message})}
$result=[ordered]@{tool='repair-sigov-database';generatedAt=[DateTimeOffset]::UtcNow.ToString('o');database=$Database;mode=$mode;force=[bool]$Force;status=$status;actions=$actions}
$result|ConvertTo-Json -Depth 6|Set-Content (Join-Path $outDir 'repair-result.json') -Encoding utf8
@('# Reparo SIGOV+','',"**Modo:** $mode  ","**Status:** $status",'',($actions|ForEach-Object{"- **$($_.action)** — aplicado=$($_.applied): $($_.detail)"}))|Set-Content (Join-Path $outDir 'repair-report.md') -Encoding utf8
$actions|ForEach-Object{"[$($_.action)] applied=$($_.applied) $($_.detail)"}|Set-Content (Join-Path $outDir 'repair.log') -Encoding utf8
Write-Host "Reparo $mode concluído: $status. Relatório: $outDir"; if($status-ne'success'){exit 2}
