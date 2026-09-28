[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$here = Split-Path $MyInvocation.MyCommand.Path -Parent
. (Join-Path $here 'jornada_lib.ps1')
$script:JDbMode='docker'
JDb "select numero, status, version, valor_estimado from sigov.compras_empresarial_requisicao where numero like 'RC-DEMO-%' order by numero;"
