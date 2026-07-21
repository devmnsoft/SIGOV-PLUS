$ErrorActionPreference = 'Stop'
$tenantId = Read-Host 'Tenant ID'
$name = Read-Host 'Nome'
$login = Read-Host 'Login'
$email = Read-Host 'E-mail'
$password = Read-Host 'Senha' -AsSecureString
$plain = [Runtime.InteropServices.Marshal]::PtrToStringBSTR([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
try {
    if ($plain.Length -lt 12 -or $plain -notmatch '[A-Z]' -or $plain -notmatch '[a-z]' -or $plain -notmatch '[0-9]') { throw 'Senha fraca: use ao menos 12 caracteres com maiúsculas, minúsculas e números.' }
    if (-not $env:SIGOV_DB_CONNECTION) { throw 'Defina SIGOV_DB_CONNECTION em variável de ambiente segura.' }
    Write-Host 'Bootstrap seguro preparado; use o serviço de hash da aplicação/CLI para persistir o administrador inicial sem registrar senha em log.'
}
finally {
    $plain = $null
}
