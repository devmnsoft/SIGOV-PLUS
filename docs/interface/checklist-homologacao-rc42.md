# Checklist de homologação — RC42

Use um tenant de homologação sem dados pessoais reais. Registre evidências e o `correlation id` em qualquer falha.

- [ ] Build da solução sem erros.
- [ ] Login, logout e mensagens genéricas de autenticação.
- [ ] Minha Central com tarefas, protocolos, documentos e notificações do tenant.
- [ ] Dashboard e navegação principal em desktop e mobile.
- [ ] Quick Create com foco, validação e fechamento corretos.
- [ ] Criar protocolo e conferir número gerado.
- [ ] Criar tarefa a partir do protocolo; conferir vínculo, responsável e notificação.
- [ ] Vincular documento GED do mesmo tenant; rejeitar documento de outro tenant.
- [ ] Dashboard, upload, listagem e detalhe do GED; conferir fallback honesto do OCR.
- [ ] Minhas tarefas, detalhes e Kanban; concluir e reabrir conforme permissão.
- [ ] Central de notificações; marcar uma e todas como lidas.
- [ ] Busca global sem resultados de outro tenant e respeitando perfil.
- [ ] Usuários, perfis e matriz de permissões; confirmar ações críticas.
- [ ] Wizard de implantação SaaS; salvar rascunho e validar pendências.
- [ ] Dashboard LGPD e trilha de auditoria com filtros e correlation id.
- [ ] Relatórios e CSV; conferir auditoria de exportação sensível.
- [ ] Layout responsivo a 360 px sem overflow horizontal grave.
- [ ] Console do navegador sem erro JavaScript.
- [ ] Links principais sem HTTP 404.
- [ ] Se houver alteração de banco, executar migrations e `script_completop.sql` duas vezes.

## Evidência mínima da jornada integrada

1. Criar um protocolo em `/Protocolo/Novo`.
2. Em `/Protocolo/{id}/CriarTarefa`, atribuir uma tarefa a outro usuário do tenant.
3. Confirmar a tarefa em `/Tarefas/Detalhes/{id}` e a notificação do responsável.
4. Em `/Protocolo/{id}/VincularDocumento`, vincular um documento GED existente.
5. Confirmar que tentativa com documento de outro tenant não cria vínculo.
6. Conferir os eventos na auditoria usando o correlation id da requisição.
