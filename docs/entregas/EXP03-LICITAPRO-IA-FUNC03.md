# EXP03 — LicitaPro IA no FUNC03

## Resultado

Entrega vertical dentro de Compras e Licitações: radar de oportunidades, fontes oficiais configuráveis (inclusive PNCP), importações versionadas, portal do fornecedor, certidões, checklist, análise por critérios explicáveis, agenda, alertas, CSV e auditoria. O contrato conquistado referencia processo, fornecedor e contrato reais.

## Permissões

`COMPRAS_LICITAPRO_DASHBOARD_VIEW`, pares `VIEW/MANAGE` de fonte, oportunidade, portal do fornecedor, documento, checklist, análise e agenda, além de `COMPRAS_LICITAPRO_RELATORIO_EXPORT` e `COMPRAS_LICITAPRO_AUDITORIA_VIEW`.

## Validação executada

- `dotnet build --no-restore`: **BLOCKED: comando dotnet build --no-restore não executado porque o SDK dotnet não está instalado no ambiente.**
- validação PostgreSQL: **BLOCKED: comando psql não executado porque o cliente psql não está instalado no ambiente.**
- manifest/checksum: validação JSON e SHA-256 local.
- smoke de rotas: inspeção estática; execução depende do runtime .NET e banco configurado.

Nenhuma credencial, payload sensível, dado real ou fallback foi incluído.
