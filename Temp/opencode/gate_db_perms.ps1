$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
. $PSScriptRoot\jornada_lib.ps1
$script:JDbMode='docker'
JDb "select id, recurso, acao from sigov.permissao where modulo='compras_empresariais' order by id;"
