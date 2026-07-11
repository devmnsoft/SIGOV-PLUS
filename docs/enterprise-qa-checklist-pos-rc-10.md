# QA Checklist Enterprise Pós-RC 10

- [ ] API sem autenticação retorna 401/403.
- [ ] API com usuário sem permissão retorna 403.
- [ ] Produção sem tenant retorna 400/401.
- [ ] Development/Homologation só usa demo com `Enterprise__AllowDemoTenantFallback=true`.
- [ ] Criar, editar, inativar e restaurar cliente.
- [ ] Criar produto, movimentar entrada/saída/ajuste e bloquear saldo negativo sem permissão.
- [ ] Aprovar proposta, gerar pedido, confirmar pedido e gerar OS.
- [ ] Iniciar/concluir OS e registrar consumo.
- [ ] Gerar OS preventiva a partir de plano.
- [ ] CSV mascara LGPD e neutraliza fórmulas iniciadas por `=`, `+`, `-` ou `@`.
- [ ] Auditoria registra usuário, tenant e correlationId.
- [ ] `enterprise-crud.js` passa em `node --check`.
- [ ] Smoke real executa ações quando ambiente Web/API estiver disponível.
