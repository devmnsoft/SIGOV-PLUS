# Testes manuais Enterprise Pós-RC 08

Execute em homologação com tenant demo seguro:
1. Login e seleção de tenant.
2. Abrir todos os menus Enterprise listados no smoke.
3. Criar/editar/inativar cliente, produto, fornecedor e ativo.
4. Criar proposta, aprovar, gerar pedido, confirmar pedido e gerar OS.
5. Agendar/iniciar/apontar/consumir peça/concluir OS.
6. Confirmar saldo atualizado e bloqueio de saldo negativo.
7. Gerar OS preventiva de plano.
8. Registrar medidor/leitura/parada.
9. Exportar CSV e confirmar LGPD mascarada.
10. Ver auditoria e validar ausência de 404/500/erro JS próprio.


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.
