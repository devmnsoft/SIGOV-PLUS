$hits = Select-String -Path 'Temp/opencode/gate_b_run5.log' -SimpleMatch '[FAIL] resposta Alfa registrada'
$ln = $hits[0].LineNumber
$start = [Math]::Max(1, $ln - 14)
Get-Content 'Temp/opencode/gate_b_run5.log' | Select-Object -Skip ($start - 1) -First 40
