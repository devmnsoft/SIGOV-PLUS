[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$here = Split-Path $MyInvocation.MyCommand.Path -Parent
. (Join-Path $here 'jornada_lib.ps1')
$sqlfile = Join-Path $here 'probe_enviar_r2.sql'
[void](docker cp "$sqlfile" 'sigov-postgres:/tmp/probe_enviar.sql')
$c = JResolveDbContract
$out = & docker exec -e PGPASSWORD="$($c.pw)" sigov-postgres psql -U "$($c.usr)" -d "$($c.db)" -A -F ' | ' -f /tmp/probe_enviar.sql 2>&1 | Out-String
Write-Host $out
[void](docker exec sigov-postgres rm -f /tmp/probe_enviar.sql)
