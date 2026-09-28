[Console]::OutputEncoding=[Text.Encoding]::UTF8
Get-Item C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_upgrade_pre2.log | Select-Object Length, LastWriteTime | Format-List
Get-Content C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_upgrade_pre2.log -Tail 8
docker exec sigov-postgres psql -U postgres -d sigov_gate_upgrade -A -F ' | ' -t -c 'select max(version) from sigov.schema_migrations' -c 'select count(*) from sigov.compras_empresarial_recebimento_divergencia' -c "select count(*) from information_schema.columns where table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo'"
"PROBE_EXIT=$LASTEXITCODE"
