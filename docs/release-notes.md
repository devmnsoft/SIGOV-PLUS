# Release notes — SIGOV PLUS 1.0.0-rc.2

## Resumo
Release Candidate para homologação técnica/comercial, com escopo congelado, matriz de módulos, smoke test, documentação operacional, backup/restore, deploy, CI/CD e validação final.

## Limitações
Módulos parciais não são prometidos como funcionais integrais. Fallback honesto permanece obrigatório. IA/OCR/Assinatura/SMTP/Integrações dependem de configuração.

## Execução
`dotnet restore`, `dotnet build`, `dotnet test`, `docker compose up -d --build` e `scripts/smoke-test-sigov.ps1`.

## Usuário de teste
Usar usuário de homologação configurado no seed/ambiente; não registrar senha real em documentação.

## Próximos passos
Executar validação em ambiente com .NET SDK/Docker, ampliar testes automatizados e fechar pendências bloqueantes.
