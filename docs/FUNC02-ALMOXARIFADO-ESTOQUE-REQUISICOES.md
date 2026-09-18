# FUNC02 — Almoxarifado, Estoque e Requisições

## 1. Escopo e Autoridade

O módulo de **Almoxarifado, Estoque e Requisições (FUNC02)** adota o PostgreSQL como única fonte de autoridade de dados, operando sempre de modo contextualizado e isolado pelo `tenant_id` e pela `entidade_id` da requisição autenticada.
Todas as transações de persistência, consultas e exportações utilizam Dapper e Npgsql em arquitetura limpa (.NET 10 / C# 14), respeitando o isolamento fail-closed e a verificação estrita de perfis e permissões persistidas (`sigov.permissao`).
Não são admitidos mocks, coleções em memória, dados hardcoded nem ORMs externos como Entity Framework Core.

---

## 2. Regras de Negócio Fundamentais

### 2.1 Materiais
- **Tipagem estrita:** Cada material deve ser classificado como `CONSUMO` ou `PERMANENTE`.
- **Unicidade de código:** O código do material é único por par `(tenant_id, entidade_id)`.
- **Parâmetros de estoque:**
  - `estoque_minimo >= 0`
  - `estoque_maximo >= estoque_minimo`
- **Validações:** Descrição e unidade de medida são de preenchimento obrigatório.

### 2.2 Locais de Armazenamento / Almoxarifados
- Cadastrados e mantidos por entidade governamental, vinculados a unidades organizacionais com nome explicativo (sem exibição de IDs puros na interface).
- Suporte a múltiplos depósitos por secretaria ou unidade descentralizada, com designação nominal de responsável e controle de status ativo/inativo.

### 2.3 Movimentações e Garantia de Saldo Não Negativo
- **Entrada (`ENTRADA`):** Operações como compra, doação, ajuste positivo ou remanejamento incrementam o saldo físico na tabela `sigov.almoxarifado_estoque`.
- **Geração de Pendência Patrimonial:** Toda entrada de material do tipo `PERMANENTE` gera atomicamente e na mesma transação exatamente um registro em `sigov.almoxarifado_pendencia_patrimonial` com status `PENDENTE` (com restrição `UNIQUE` em `movimentacao_id`). Não se admite tombamento incompleto direto no almoxarifado.
- **Saída (`SAIDA`):** Operações de consumo, perda, descarte ou ajuste negativo são validadas de forma fail-closed. Nenhuma saída ou atendimento pode debitar quantidade maior que o saldo disponível (`quantidade_disponivel >= quantidade_solicitada`), garantindo que o estoque jamais fique negativo (`CHECK (saldo >= 0)`).
- **Rastreabilidade:** Toda movimentação registra saldo anterior, quantidade movimentada, saldo posterior, usuário autor e log de auditoria na mesma transação.

### 2.4 Fluxo de Requisições Internas (Distribuição)
As requisições internas seguem um ciclo de vida formal, com validações de transição de estado e gravação de histórico com justificativa:
```
[RASCUNHO]
    │
    ▼ (Enviar)
[ENVIADA]
    ├──► (Aprovar / Autorizar) ──► [APROVADA]
    │                                  │
    │                                  ├──► (Atendimento Integral / Parcial) ──► [ATENDIDA]
    │                                  │
    │                                  └──► (Cancelar com justificativa) ──────► [CANCELADA]
    │
    ├──► (Rejeitar com justificativa) ─────────────────────────────────────────► [REJEITADA]
    │
    └──► (Cancelar com justificativa) ─────────────────────────────────────────► [CANCELADA]
```
- **Atendimento de Requisição:** No atendimento (seja direto ou por remessas de separação/conferência), o sistema efetua lock das linhas de estoque (`FOR UPDATE`), valida a disponibilidade de todos os itens e só então executa o débito do saldo e confirma o status `ATENDIDA` na mesma transação atômica.
- **Histórico e Auditoria:** Cada mudança de estado grava um registro detalhado em `sigov.almoxarifado_requisicao_historico`.

### 2.5 Exportação CSV Segura (LGPD e Proteção de Planilhas)
- Exportações de catálogo de materiais, saldos e movimentações são geradas com codificação **UTF-8 com BOM** para compatibilidade com Microsoft Excel e LibreOffice.
- Limite máximo estrito de **5.000 registros** por exportação.
- Sanitização contra ataques de injeção de fórmulas (*CSV Formula Injection*): qualquer campo iniciado pelos caracteres `=`, `+`, `-`, `@`, `\t` ou `\r` é neutralizado com aspas e prefixo seguro (`\t`).
- Em conformidade com a LGPD e a política de privacidade, os relatórios CSV do almoxarifado não expõem nomes, CPFs ou logins de usuários/responsáveis.

---

## 3. Integração Multiesferas com FUNC01 (Patrimônio)

Para preservar a integridade contábil e patrimonial:
1. **Entrada de permanente sem tombamento cego:** Materiais permanentes recebidos no almoxarifado não são inseridos prematuramente no tombamento de bens patrimoniais, evitando registros com campos obrigatórios ausentes (como plaqueta, estado de conservação, valor residual, conta contábil e fotos).
2. **Fila de Pendências Patrimoniais:** O almoxarifado disponibiliza a tela de `/Almoxarifado/Pendencias` e alertas no Dashboard com todas as entradas pendentes de regularização patrimonial.
3. **Conclusão no Módulo de Patrimônio:** O operador acessa o formulário de cadastro em `/Patrimonio/Bens/Novo?pendenciaId={id}`. O sistema carrega os dados do material e da movimentação de origem. Ao salvar o bem, o serviço concilia atomicamente a pendência em `sigov.almoxarifado_pendencia_patrimonial`, registrando o `patrimonio_bem_id`, marcando o status como `CONCLUIDA` e carimbando a data de resolução `resolved_at`.
4. **Fechamento de Regras do FUNC01:**
   - Edição de bens com status `BAIXADO` é estritamente bloqueada no serviço (`PatrimonioService.EditarBemAsync`) e na action do controlador (`PatrimonioController.Editar`).
   - Botão de solicitação de transferência/movimentação em `/Patrimonio/Bens/Detalhe/{id}` é desabilitado quando o bem possui Ordens de Serviço impeditivas nos status `APROVADA`, `EM_EXECUCAO` ou `AGUARDANDO_PECA`, acompanhado de alerta contextual explicativo.
   - O inventário patrimonial realiza join com `sigov.unidade_organizacional` e `sigov.usuario` para exibir `UnidadeNome` e `ResponsavelNome` na visualização.
   - Rotas de Depreciação, Imóveis e Relatórios mantêm redirecionamento explícito e amigável para o catálogo consolidado de Ativos.

---

## 4. Padrões de Interface e Experiência do Usuário (Design System)

Todas as telas do módulo `/Almoxarifado` seguem o design system institucional padronizado em `patrimonio.css` e `almoxarifado.css`:
- **Tipografia e Hierarquia:** Utilização de fontes modernas, cards de indicadores com destaque numérico e pesos tipográficos consistentes.
- **Ajuda Contextual ("Como usar esta tela"):** Todas as páginas contam com bloco colapsável `<details class="how-to">` orientando o usuário sobre as regras operacionais daquela tela.
- **Confirmação em Ações Críticas:** Botões que disparam mutação de estado, baixa de estoque, cancelamentos ou aprovações utilizam diálogos `data-confirm` ou modais para prevenção de duplo clique e envios acidentais.
- **Seleção Sem IDs Digitados:** Seletores de unidade e almoxarifado apresentam os nomes amigáveis das unidades organizacionais, nunca obrigando o operador a digitar chaves numéricas.
- **Empty States:** Mensagens informativas claras com orientações quando a consulta ou filtro não retornar registros.

---

## 5. Rotas do Módulo Web e API

### 5.1 Web (MVC / Razor)
- `/Almoxarifado`: Dashboard com indicadores de volume, movimentações recentes, itens em ponto de pedido e alerta de pendências patrimoniais.
- `/Almoxarifado/Materiais`: Consulta do catálogo com filtros de tipo, estado e exportação CSV.
- `/Almoxarifado/Materiais/Novo` e `/Editar/{id}`: Cadastro e manutenção de itens.
- `/Almoxarifado/Locais`: Gestão de almoxarifados e depósitos por unidade organizacional.
- `/Almoxarifado/Estoque`: Painel de saldos físicos com filtros por local e alerta de estoque mínimo.
- `/Almoxarifado/Movimentacoes/NovaEntrada` e `/NovaSaida`: Lançamentos de entradas e saídas com validações de saldo e materiais permanentes.
- `/Almoxarifado/Requisicoes`: Rastreamento de pedidos internos de materiais.
- `/Almoxarifado/Requisicoes/Nova`: Abertura de solicitação por unidade demandante.
- `/Almoxarifado/Requisicoes/Detalhe/{id}`: Detalhe do pedido, autorização, atendimento e conferência.
- `/Almoxarifado/Pendencias`: Central de conciliação de materiais permanentes aguardando tombamento.
- `/Almoxarifado/Reposicao`: Análise preditiva de ponto de pedido e sugestões explicáveis de aquisição.
- `/Almoxarifado/Transferencias`: Gestão de remanejamento físico entre almoxarifados.

### 5.2 API REST
- `GET /api/almoxarifado/dashboard`: Métricas resumidas do tenant/entidade.
- `GET /api/almoxarifado/materiais`: Listagem paginada de materiais.
- `POST /api/almoxarifado/materiais`: Cadastro de material.
- `POST /api/almoxarifado/movimentacoes/entrada`: Registro de entrada em estoque.
- `POST /api/almoxarifado/movimentacoes/saida`: Registro de saída em estoque com checagem de saldo.
- `POST /api/almoxarifado/requisicoes`: Abertura de requisição.
- `POST /api/almoxarifado/requisicoes/{id}/{acao}`: Transições de status (`enviar`, `aprovar`, `rejeitar`, `atender`, `cancelar`).
