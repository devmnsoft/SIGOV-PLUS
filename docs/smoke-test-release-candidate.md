# Smoke test Release Candidate

O script `scripts/smoke-test-sigov.ps1` foi revisado para testar rotas principais Web/API, registrar status HTTP, continuar após falhas e gerar resumo final.

Resultado desta execução: pendente por ausência de `dotnet`/`docker` e serviços locais ativos no ambiente do agente.

## Atualização Pós-RC

- Funcional real: schema homologável de API key, webhook, outbox, protocolo, GED, workflow, tarefas, notificações e validação pública.
- Parcial: telas e actions ainda não conectadas ao serviço de persistência devem manter fallback honesto.
- Dependente de provedor/configuração: assinatura oficial, OCR, storage externo e entregas HTTP reais.
- Não disponível: simulação de assinatura oficial, OCR, pagamento/empenho ou exposição de dados sensíveis sem máscara.
