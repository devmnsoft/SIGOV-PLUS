[CmdletBinding()]
param([string]$HostName='localhost',[int]$Port=5432,[string]$Database='sigov',[string]$User='postgres',[string]$PsqlPath='psql',[string]$SchemaName='sigov',[string]$OutputDirectory='artifacts/database/validation')
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot;$dir=if([IO.Path]::IsPathRooted($OutputDirectory)){$OutputDirectory}else{Join-Path $root $OutputDirectory};New-Item -ItemType Directory -Force $dir|Out-Null
if($SchemaName-notmatch'^[A-Za-z_][A-Za-z0-9_]{0,62}$'){throw 'SchemaName inválido.'}
$items=[Collections.Generic.List[object]]::new();$failed=$false
foreach($file in Get-ChildItem (Join-Path $root 'database/postgres/validation') -Filter '*.sql'|Sort-Object Name){
  $out=& $PsqlPath -X -q -v ON_ERROR_STOP=1 -v "schema=$SchemaName" -h $HostName -p $Port -U $User -d $Database -f $file.FullName 2>&1
  $ok=$LASTEXITCODE-eq 0;if(-not$ok){$failed=$true};$items.Add([ordered]@{file=$file.Name;status=if($ok){'OK'}else{'ERROR'};message=($out-join "`n")})
}
$result=[ordered]@{tool='validate-sigov-runtime';generatedAt=[DateTimeOffset]::UtcNow.ToString('o');database=$Database;status=if($failed){'FAILED'}else{'HEALTHY'};validations=$items}
$result|ConvertTo-Json -Depth 6|Set-Content (Join-Path $dir 'validation-result.json') -Encoding utf8
@('# Validação runtime SIGOV+','',"**Status:** $($result.status)",'',($items|ForEach-Object{"- **$($_.status)** ``$($_.file)``"}))|Set-Content (Join-Path $dir 'validation-report.md') -Encoding utf8
$items|ForEach-Object{"[$($_.status)] $($_.file)`n$($_.message)"}|Set-Content (Join-Path $dir 'validation.log') -Encoding utf8
Write-Host "Validação runtime: $($result.status). Relatório: $dir";if($failed){exit 1}
