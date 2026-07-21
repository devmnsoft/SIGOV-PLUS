# Plano de migração de runtime Pós-RC 21

O Pós-RC 21 mantém o runtime alvo em .NET 6 para estabilizar build, migrations e execução standalone sem introduzir migração de plataforma na mesma rodada.

## Diretrizes

- O SDK local/CI deve obedecer ao `global.json` com versão `6.0.428` e `rollForward: latestPatch`.
- A migração para runtime suportado deve ocorrer em ciclo próprio, com inventário de pacotes, atualização de imagens, testes de compatibilidade e validação de produção.
- Nenhum fallback silencioso para SDK 10 deve ser aceito nos jobs obrigatórios.
