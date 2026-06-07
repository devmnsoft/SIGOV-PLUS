# Plano de rollback - sigov v1.0.0

## Objetivo

Garantir retorno controlado para a versão anterior aprovada sem executar restore automaticamente.

## Pré-condições

- `SIGOV_PREVIOUS_VERSION` informado.
- `SIGOV_CURRENT_VERSION=v1.0.0`.
- `SIGOV_LAST_BACKUP_FILE` existente.
- `SIGOV_LAST_BACKUP_CHECKSUM` validado.
- Imagem/tag anterior informada por `SIGOV_PREVIOUS_DOCKER_IMAGE`, quando aplicável.
- `scripts/restore-db.ps1` disponível e protegido por confirmação.

## Procedimento

1. Parar tráfego externo no reverse proxy ou balanceador.
2. Preservar logs e métricas da falha.
3. Reverter imagem/tag para a versão anterior.
4. Avaliar impacto de migrations aplicadas. Rollback de migration é manual e exige plano aprovado.
5. Se restore for necessário, executar somente com confirmação `RESTORE_PRODUCTION_SIGOV`.
6. Validar `/api/health/live`, `/api/health/ready`, `/api/health/db`, `/api/health/outbox` e `/api/health/version`.

## Validação

```powershell
pwsh scripts/rollback-check.ps1 -AllowWarnings
```
