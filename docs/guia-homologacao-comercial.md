# Guia de homologação comercial — Pós-RC 04

Este guia descreve um roteiro demonstrável e seguro do SIGOV PLUS para homologação técnica/comercial. O ambiente usa dados fictícios, mascaráveis e identificados como demo.

## Subida do ambiente

```powershell
dotnet clean sigov.sln
dotnet restore sigov.sln
dotnet build sigov.sln
dotnet test sigov.sln
docker compose down
docker compose build --no-cache
docker compose up -d
docker compose ps
pwsh -NoProfile -File scripts/apply-demo-seed.ps1
pwsh -NoProfile -File scripts/smoke-test-sigov.ps1
```

## Usuários demo

| Usuário | Perfil | Observação |
|---|---|---|
| admin.geral | Admin geral | Homologação, sem senha real em seed |
| admin.tenant | Admin tenant | Gestão do tenant demo |
| coord.protocolo | Coordenação de protocolo | Tramitação e acompanhamento |
| servidor.protocolo | Servidor de protocolo | Tarefas operacionais |
| operador.ged | Operador GED | Upload, hash e validação pública |
| consulta | Consulta | Visualização limitada |

## Fluxo comercial sugerido

1. Abrir `/Dashboard` e demonstrar KPIs reais do PostgreSQL.
2. Abrir `/MinhaCentral` para tarefas/notificações do usuário.
3. Criar protocolo em `/Protocolo/Novo`.
4. Tramitar protocolo e mostrar timeline.
5. Anexar documento em `/Ged/NovoDocumento`.
6. Validar documento público em `/ValidarDocumento` usando `PUB-DEMO-001` ou hash.
7. Pesquisar em `/Busca?q=protocolo`.
8. Exportar CSV em `/Relatorios` e evidenciar mascaramento LGPD.
9. Mostrar `/Seguranca/ApiKeys` sem token claro persistido.
10. Mostrar `/Integracoes/Webhooks` e histórico/outbox.
11. Abrir `/Poc` e associar evidências reais.

## Funcional real, parcial e dependências

- Real: protocolo/GED/workflow/tarefas/notificações/outbox quando o schema Pós-RC está aplicado.
- Real com provedor interno/local: CSV, validação pública e API key/webhook por dados persistidos.
- Parcial: PDF/DOCX de POC quando não houver infraestrutura de geração real.
- Não simulado: ICP-Brasil, Gov.br e OCR.

## Evidências Pós-RC 05 para demonstração comercial

Antes da demonstração, anexar: workflow CI verde, Docker Compose saudável, seed demo aplicado sem duplicidade, smoke Markdown/JSON, checklist Go-Live, checklist LGPD e pacote de release gerado. Não apresentar como real qualquer integração dependente de provedor que não tenha contrato/configuração ativa.
