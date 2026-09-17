# FUNC01 — Patrimônio, Inventário e Responsabilidade Patrimonial

## Escopo funcional

A trilha FUNC01 entrega cadastro e tombamento de bens, edição, movimentação entre unidade/responsável/localização, baixa, inventário físico, divergências automáticas, dashboard e CSV seguro. Os fluxos usam exclusivamente PostgreSQL via Dapper, com `tenant_id` obrigatório e autorização persistida fail-closed.

Esta trilha é avanço funcional paralelo de produto: **não promove a RC50.68**, que continua **BLOCKED** por ambiente/CI, e **não inicia nem promove a RC50.69**.

## Preparação

1. Configure `ConnectionStrings__DefaultConnection` para PostgreSQL 16+.
2. Aplique `script_completop.sql` com `psql -v ON_ERROR_STOP=1 -f script_completop.sql`.
3. Conceda as permissões persistidas de `patrimonio.*` aos perfis operacionais. A migration concede todas apenas ao perfil sistêmico `SUPERADMIN`, quando existente.
4. Acesse `/Patrimonio`; a API equivalente está em `/api/patrimonio`.

## Jornadas

- **Bem:** Bens → Tombar bem → preencher identificação, aquisição, conservação e responsabilidade → salvar.
- **Movimentação:** detalhe do bem → informar destino e justificativa → confirmar. Bem baixado é bloqueado.
- **Baixa:** detalhe do bem ativo → tipo, data, valor residual e justificativa → confirmar. A operação muda a situação para `BAIXADO` na mesma transação e impede nova movimentação ou termo.
- **Inventário:** Inventários → abrir por unidade/responsável ou geral → conferir cada item → fechar quando não houver item pendente.
- **Divergência:** não localização ou diferença de estado/localização em relação ao cadastro marca o item automaticamente.
- **Termo de responsabilidade:** o detalhe do bem contém a responsabilidade vigente e pode ser impresso pelo navegador; movimentações preservam a cadeia auditável de origem/destino.

## Segurança, auditoria e LGPD

Todas as rotas exigem usuário autenticado e decisão do avaliador persistido por recurso/ação. Escritas usam queries parametrizadas, transação e `sigov.patrimonio_auditoria` com antes/depois, usuário e correlation ID. O CSV limita 100 linhas por solicitação, não inclui nome/e-mail/documentos do responsável e neutraliza células iniciadas por caracteres de fórmula.

Ausência de schema ou contexto não simula sucesso: a operação falha explicitamente. Não há catálogo em memória, mock ou fallback.

## Correção CS0103 e evolução de template

A operação de transferência persistia `status=@Novo` com inicializador anônimo `new { Novo }`. Em C# isso exige um identificador `Novo` no escopo; a variável existente era `novo`. O parâmetro Dapper passou a ser `Novo = novo`, preservando o nome `@Novo` no SQL.

As telas de FUNC01 passaram a exibir nome de unidade e responsável (não o ID técnico), filtros e abertura de inventário por listas persistidas, paginação da consulta, formulário de baixa no detalhe, validação por campo, confirmação nas ações irreversíveis e o bloco **Como usar esta tela**. O CSV inclui o nome da unidade e permanece limitado a 100 linhas, sem nome, e-mail ou documento do responsável.

## Regras de negócio reforçadas na UI

- Tipo de bem e estado de conservação aceitam somente catálogo fechado (`MOVEL`, `IMOVEL`, `VEICULO`, `EQUIPAMENTO`, `INTANGIVEL`, `SEMOVENTES`, `INFRAESTRUTURA` / `NOVO`–`INSERVIVEL`).
- Bem com situação `BAIXADO` não é editável na Web nem no serviço.
- Tipo de baixa aceita somente `INSERVIVEL`, `DOACAO`, `ALIENACAO`, `EXTRAVIO`, `SINISTRO` e `TRANSFERENCIA_EXTERNA`.
- Inventário lista unidade e responsável do escopo pelo nome. O botão Fechar permanece desabilitado enquanto houver item sem conferência.
- Conferência de bem não localizado exige descrição da divergência.
- A listagem de bens filtra também por categoria persistida.
