[CmdletBinding()]
param(
    [string]$Login = 'admin',
    [string]$Password = 'SigovDevLocal!2026',
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'sigov',
    [string]$User = 'sigov',
    [string]$PsqlPath = 'psql'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-SigovPasswordHash {
    param([Parameter(Mandatory)][string]$PlainText, [Parameter(Mandatory)][string]$EncodedHash)
    try {
        $parts = $EncodedHash.Split('$')
        if ($parts.Count -ne 4 -or $parts[0] -cne 'SIGOV_PBKDF2_V1') { return $false }
        $iterations = 0
        if (-not [int]::TryParse($parts[1], [ref]$iterations) -or $iterations -lt 100000 -or $iterations -gt 1000000) { return $false }
        $salt = [Convert]::FromBase64String($parts[2])
        $expected = [Convert]::FromBase64String($parts[3])
        if ($salt.Length -ne 16 -or $expected.Length -ne 32) { return $false }
        $pbkdf2 = [Security.Cryptography.Rfc2898DeriveBytes]::new($PlainText, $salt, $iterations, [Security.Cryptography.HashAlgorithmName]::SHA256)
        try { $actual = $pbkdf2.GetBytes(32) } finally { $pbkdf2.Dispose() }
        return [Security.Cryptography.CryptographicOperations]::FixedTimeEquals($actual, $expected)
    }
    catch { return $false }
}

function Test-SigovPasswordHashFormat {
    param([Parameter(Mandatory)][string]$EncodedHash)
    try {
        $parts = $EncodedHash.Split('$')
        $iterations = 0
        if ($parts.Count -ne 4 -or $parts[0] -cne 'SIGOV_PBKDF2_V1' -or
            -not [int]::TryParse($parts[1], [ref]$iterations) -or $iterations -lt 100000 -or $iterations -gt 1000000) { return $false }
        return ([Convert]::FromBase64String($parts[2])).Length -eq 16 -and
               ([Convert]::FromBase64String($parts[3])).Length -eq 32
    }
    catch { return $false }
}

if ([string]::IsNullOrWhiteSpace($env:SIGOV_DB_PASSWORD)) { throw 'Defina SIGOV_DB_PASSWORD para consultar o banco local.' }
$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $env:SIGOV_DB_PASSWORD
    $escaped = $Login.Replace("'", "''")
    $sql = @"
select coalesce(json_agg(row_to_json(q)), '[]'::json)::text from (
 select u.id, u.tenant_id, coalesce(t.ativo,true) tenant_ativo, coalesce(t.is_deleted,false) tenant_is_deleted,
 u.login, coalesce(u.email,'') email, u.ativo, u.bloqueado, u.is_deleted,
 coalesce(u.deve_alterar_senha,false) deve_alterar_senha, u.tipo_usuario, u.senha_hash,
 exists(select 1 from sigov.usuario_grupo ug where ug.usuario_id=u.id and not ug.is_deleted) possui_grupo,
 exists(select 1 from sigov.usuario_grupo ug join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and not gp.is_deleted where ug.usuario_id=u.id and not ug.is_deleted) possui_perfil,
 exists(select 1 from sigov.usuario_grupo ug join sigov.grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id and not gp.is_deleted join sigov.perfil_permissao pp on pp.perfil_acesso_id=gp.perfil_acesso_id where ug.usuario_id=u.id and not ug.is_deleted) possui_permissoes,
 (u.entidade_id is not null or exists(select 1 from sigov.usuario_entidade ue where ue.usuario_id=u.id and ue.ativo)) possui_entidade,
 (u.exercicio_id is not null or exists(select 1 from sigov.usuario_exercicio ux where ux.usuario_id=u.id and ux.ativo)) possui_exercicio
 from sigov.usuario u left join sigov.tenant t on t.id=u.tenant_id
 where lower(u.login)=lower('$escaped') or lower(coalesce(u.email,''))=lower('$escaped') order by u.id desc
) q;
"@
    $raw = & $PsqlPath -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $User -d $Database -Atqc $sql
    if ($LASTEXITCODE -ne 0) { throw "psql falhou com código $LASTEXITCODE." }
    $records = @($raw | ConvertFrom-Json)
}
finally { $env:PGPASSWORD = $previousPassword }

Write-Host "Banco: $Database em ${HostName}:$Port (usuário $User)"
Write-Host "Usuários encontrados: $($records.Count)"
if ($records.Count -eq 0) { throw "Nenhum usuário corresponde a '$Login'." }
if ($records.Count -gt 1) { throw "Há $($records.Count) usuários conflitantes para '$Login'." }

$admin = $records[0]
$hash = [string]$admin.senha_hash
$parts = $hash.Split('$')
$formatValid = Test-SigovPasswordHashFormat -EncodedHash $hash
$passwordMatches = Test-SigovPasswordHash -PlainText $Password -EncodedHash $hash
[ordered]@{
    id=$admin.id; tenant_id=$admin.tenant_id; tenant_ativo=$admin.tenant_ativo; tenant_is_deleted=$admin.tenant_is_deleted
    login=$admin.login; email=$admin.email; ativo=$admin.ativo; bloqueado=$admin.bloqueado; is_deleted=$admin.is_deleted
    deve_alterar_senha=$admin.deve_alterar_senha; tipo_usuario=$admin.tipo_usuario
    hash_prefix=if($parts.Count){$parts[0]}else{'INVALIDO'}; hash_formato_valido=$formatValid; senha_confere=$passwordMatches
    possui_grupo=$admin.possui_grupo; possui_perfil=$admin.possui_perfil; possui_permissoes=$admin.possui_permissoes
    possui_entidade=$admin.possui_entidade; possui_exercicio=$admin.possui_exercicio
} | Format-List | Out-Host

if (-not $admin.ativo -or $admin.bloqueado -or $admin.is_deleted) { throw 'O administrador não está ativo e desbloqueado.' }
if (-not $admin.tenant_ativo -or $admin.tenant_is_deleted) { throw 'O tenant do administrador não está ativo.' }
if (-not $formatValid -or -not $passwordMatches) { throw 'O hash PBKDF2 ou a senha informada é inválido.' }
if (-not $admin.possui_grupo -or -not $admin.possui_perfil -or -not $admin.possui_permissoes) { throw 'Faltam vínculos administrativos obrigatórios.' }
Write-Host 'Status: válido' -ForegroundColor Green
