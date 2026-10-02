$h = (Get-FileHash -Algorithm SHA256 C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\20260925120000_compras_devolucao_destinacao_final.sql).Hash.ToLower()
Write-Output $h
Write-Output 'expect 2d2c70cc67c932e086c6eca84101110f7839ea8af604c92c18ed9708106fe5ff'
