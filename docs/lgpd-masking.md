# LGPD e mascaramento

## Catálogos centrais

O código possui catálogos centrais para dados pessoais, sensíveis e secrets em `Sigov.Application.Lgpd`.

## Máscaras

`LgpdMaskingService` mascara CPF, CNPJ, e-mail, telefone, tokens/secrets e dados de saúde. Exports e logs devem chamar a política central de mascaramento por padrão.

## Auditoria de acesso

`ILgpdAccessLogger` registra acesso a dado pessoal com operação, campo, tenant e correlation id.
