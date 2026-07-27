# Diagnóstico inicial — Pós-RC 27B.1

- SHA-base verificado: `ee6496957b02829c60aa476ac967ed1dc95a8528`.
- Workflow informado: run `30209603139` (workflow 298), com 533 testes e 54 falhas segundo o relatório de entrada.
- O checkout fornecido não possui remoto Git configurado, GitHub CLI, SDK .NET, PowerShell nem Docker. Portanto, os logs/TRX do run não puderam ser obtidos localmente e os FQNs não são inventados neste documento.
- Defeito reproduzido por inspeção: a migration de IA usava `on conflict(chave)`, embora o contrato seja único por `(modulo, chave)`.
- Riscos ainda sujeitos à CI: baseline gerado, execução PostgreSQL, runtime, imagens Docker e testes browser.

## Plano de triagem

Os casos devem ser importados do TRX do run, preservando FQN e projeto, e classificados nas categorias definidas em `test-triage.json`. Nenhum teste foi removido ou desabilitado.
