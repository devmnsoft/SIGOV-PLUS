# FUNC02 — Almoxarifado, Estoque e Requisições

## Escopo e autoridade

O módulo usa PostgreSQL como única fonte operacional, sempre filtrado pelo `tenant_id` e pela `entidade_id` do contexto autenticado. Todas as escritas e exportações passam pelo avaliador RBAC persistente; ausência de contexto, schema ou concessão falha fechada. A persistência é Dapper/Npgsql, sem EF Core, mocks ou catálogo em memória.

## Jornadas

- **Materiais:** consulta com busca/status, cadastro e edição/inativação pelo contrato de serviço. Código é único por tenant/entidade; descrição/unidade são obrigatórias; tipo aceita `CONSUMO` ou `PERMANENTE`; mínimo é não negativo e máximo não pode ser inferior ao mínimo.
- **Locais:** cadastro/listagem de depósitos por entidade, com unidade opcional, responsável nominal e estado ativo.
- **Entrada:** compra, doação, ajuste positivo ou transferência recebida incrementa o saldo e grava movimento e auditoria na mesma transação.
- **Saída:** consumo, perda, ajuste negativo ou transferência bloqueia saldo insuficiente, mantém `quantidade >= 0` também por check do banco e registra saldo antes/depois.
- **Requisição:** nasce em `RASCUNHO`, pode ser enviada, aprovada/rejeitada, atendida ou cancelada. O atendimento bloqueia todas as linhas de estoque, valida todos os itens e só então baixa os saldos e confirma `ATENDIDA`, na mesma transação. Cada mudança gera histórico.
- **Dashboard:** consulta contagens reais, movimentos recentes, baixo estoque e pendências patrimoniais.
- **CSV:** catálogo, saldo e movimentos, com UTF-8 BOM, cabeçalho, limite de 5.000 registros, neutralização de fórmula e auditoria; não exporta usuário ou responsável.

## Integração com Patrimônio

Não existe contrato seguro no FUNC01 para tombar automaticamente um bem sem seus campos obrigatórios. Por isso cada **entrada** de material `PERMANENTE` cria exatamente uma `almoxarifado_pendencia_patrimonial`, protegida por unique da movimentação. O dashboard exibe a quantidade pendente. O operador consulta a entrada e conclui o tombamento no módulo `/Patrimonio/Bens/Novo`; a conciliação da pendência deve registrar o `patrimonio_bem_id`, sem criar bem incompleto ou duplicado.

## Rotas

MVC: `/Almoxarifado`, `/Materiais`, `/Materiais/Novo`, `/Locais`, `/Estoque`, `/Movimentacoes/NovaEntrada`, `/Movimentacoes/NovaSaida`, `/Requisicoes`, `/Requisicoes/Nova` e `/Requisicoes/Detalhe/{id}` sob o prefixo `/Almoxarifado`.

API: `GET dashboard/materiais/requisicoes`, `POST materiais`, entradas/saídas, criação de requisição e `POST requisicoes/{id}/{enviar|aprovar|rejeitar|atender|cancelar}`, sob `/api/almoxarifado`.

## Permissões

`almoxarifado.dashboard.visualizar`, `almoxarifado.material.visualizar`, `.criar`, `.editar`, `almoxarifado.estoque.visualizar`, `almoxarifado.movimentacao.entrada`, `.saida`, `almoxarifado.requisicao.visualizar`, `.criar`, `.aprovar`, `.atender` e `almoxarifado.exportar`. A migration concede-as idempotentemente apenas ao perfil sistêmico `SUPERADMIN`; demais concessões continuam sob a administração persistente.
