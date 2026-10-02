$p = 'C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\20260930120000_compras_aprovacao_causa_bloqueio_e_resultado_idempotencia.sql'
$bytes = [IO.File]::ReadAllBytes($p)
$text = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF).Replace("`r`n", "`n").Replace("`r", "`n")
$sha = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($text))
[BitConverter]::ToString($sha).Replace('-','').ToLowerInvariant()
