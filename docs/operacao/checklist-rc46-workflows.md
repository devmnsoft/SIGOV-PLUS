# Homologação RC46 — workflows operacionais

## Pré-condições

- [ ] PostgreSQL 16 recebeu a migration `20260807120000_rc46_operacao_integrada.sql` duas vezes sem erro.
- [ ] Usuário, tenant, entidade e exercício estão presentes no contexto autenticado.
- [ ] Perfis possuem apenas as permissões necessárias de Protocolo, GED e Tarefas.

## Jornada integrada

- [ ] Criar protocolo e confirmar número único no formato `000001/AAAA`.
- [ ] Confirmar que interessado/documento aparecem mascarados na listagem.
- [ ] Tramitar com observação; tentativa sem observação deve falhar.
- [ ] Conferir mudança para `EM_TRAMITACAO`, notificação, tarefa e timeline com correlation id.
- [ ] Vincular documento GED pertencente ao mesmo tenant; vínculo entre tenants deve falhar.
- [ ] Criar tarefa vinculada e confirmar notificação ao responsável.
- [ ] Concluir com justificativa; tentativa sem justificativa deve falhar.
- [ ] Arquivar apenas com permissão e justificativa.
- [ ] Confirmar trilhas em Auditoria e eventos sensíveis em LGPD.

## Regressão e experiência

- [ ] Busca, Minha Central e Dashboard exibem dados reais ou vazio honesto.
- [ ] Rotas POST rejeitam antiforgery ausente e usuário sem permissão.
- [ ] Fluxos principais funcionam a 320 px sem rolagem horizontal grave.
- [ ] Console do navegador não apresenta erros e o foco permanece visível.
- [ ] Exportação CSV respeita filtros e isolamento do tenant.

