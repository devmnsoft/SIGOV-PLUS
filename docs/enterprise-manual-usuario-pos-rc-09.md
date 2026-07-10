# Manual do Usuário Enterprise Pós-RC 09

## Navegação

Acesse o menu Enterprise e abra Comércio, Ordem de Serviço, Estoque, Compras ou Industrial. Cada tela possui cabeçalho de jornada, KPIs, filtros, tabela, paginação, botão Novo, ações por linha, exportação CSV e aviso LGPD.

## Operação padrão de CRUD

1. Use **Filtrar** para buscar por nome, código ou status.
2. Use **Novo** para cadastrar um registro fictício de homologação.
3. Use **Detalhes** para conferir ID, status e orientação de auditoria sem expor dados sensíveis.
4. Use **Editar** para atualizar nome/status e salvar com auditoria.
5. Use **Inativar** para soft delete após confirmação.
6. Use **Restaurar** para reativar registros inativados quando a API retornar o item.
7. Use **Exportar CSV** para baixar relatório mascarado por tenant.

## Jornadas principais

- Comercial: cliente, proposta, aprovação, pedido, confirmação e OS.
- OS: agenda, início, checklist/apontamento, consumo de peça e conclusão.
- Estoque/Compras: fornecedor, pedido, recebimento, entrada, requisição e saída.
- Industrial: ativo, plano preventivo, OS preventiva, medidor/leitura e parada.

## LGPD e segurança

Listagens e CSV exibem documento, e-mail e telefone mascarados. Não utilize dados reais de pessoas em homologação; use massa fictícia do seed Enterprise.
