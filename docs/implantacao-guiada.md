# Implantação guiada SIGOV

## Fluxo de implantação

A implantação guiada é iniciada automaticamente quando a API consulta ou inicia onboarding para um tenant sem implantação. A tabela `sigov.saas_implantacao` mantém status, responsável, previsão, conclusão e percentual.

## Checklist padrão

1. Dados do cliente cadastrados.
2. Plano selecionado.
3. Módulos contratados.
4. White label configurado.
5. Usuário administrador do tenant criado.
6. Perfis revisados.
7. Permissões revisadas.
8. Migração de dados planejada.
9. Parametrizações iniciais concluídas.
10. Validação com cliente realizada.
11. Treinamento inicial agendado.
12. Ambiente liberado.

## Responsável

A implantação possui responsável nome/e-mail e data prevista, editáveis pela API `/api/saas/implantacoes/{implantacaoId}` e pela tela `/Saas/Implantacao`.

## Percentual

O percentual é calculado por itens concluídos sobre o total de itens da implantação. Concluir ou reabrir item recalcula a implantação.

## Conclusão

A conclusão exige todos os itens obrigatórios concluídos. Caso exista pendência obrigatória, a API retorna erro padronizado e não altera o status.

## Bloqueios

A implantação não remove dados nem altera módulos. Bloqueios comerciais e operacionais são aplicados pela assinatura e pelo validador de limites do plano.
