# RC51.05 — planejamento explicável de reposição

Data: 2026-09-16. Estado: **PARCIAL, SEM HOMOLOGAÇÃO RUNTIME**.

## Diagnóstico e fontes

Foram consultados `AGENTS.md`, `README.md`, `docs/execucao/CODEX_EXECUTION_CONTRACT.md`, `docs/FUNC02-ALMOXARIFADO-ESTOQUE-REQUISICOES.md`, `docs/padroes-forms.md`, `docs/padroes-ui.md`, as ADRs de autenticação e catálogo SaaS, os workflows e as entregas RC51.03/RC51.04. Branch inicial `work`, HEAD `5cd7aaa56f899a9d153bc1433df5f718e69942cc`, árvore limpa, sem remoto/upstream. O SDK exigido é 10.0.100; `dotnet`, `psql`, PostgreSQL, Docker, PowerShell e navegador não estão instalados no ambiente.

| Funcionalidade | Implementação existente | Lacuna | Alteração | Verificação |
|---|---|---|---|---|
| saldo e demanda | saldo canônico e requisição/entrega parcial | sem projeção única | projeção desconta uma vez o saldo ainda não atendido | análise SQL e gates estáticos |
| políticas | mínimo global no material | não distinguia almoxarifado, vigência ou lote de compra | política versionada por material/local, auditada | constraints e pós-condição |
| entradas | transferências e compras parciais | datas/fontes não formam previsão confiável comum | trânsito é sinalizado, mas não somado sem data confiável | cenário conservador explícito |
| painel | estoque crítico não paginado | sem explicação/filtros/contadores totais | painel server-side, estável e contextual | Razor/build pendente de SDK |
| encaminhamento | requisição empresarial UUID e compra pública bigint coexistem | não há adaptador canônico aprovado entre catálogos | não foi criado pedido nem integração paralela | próximo backlog |

## Semântica aplicada

`saldo físico` é `almoxarifado_estoque.quantidade`. `compromissos` é a parte solicitada e ainda não atendida das requisições ENVIADA/APROVADA; separações são subconjunto desse valor e não são somadas novamente. A saída confirmada já reduz o físico. Transferência expedida ao destino é `em trânsito`, mas, sem previsão confiável no contrato atual, vale zero como **entrada elegível** e gera impedimento. Bloqueio/quarentena segue sem representação canônica e impede declarar disponibilidade plena.

A expressão apresentada é: **físico + entradas elegíveis − demandas ainda não atendidas = posição projetada**. Se política estiver ausente, inativa ou fora da vigência, o painel retorna “análise incompleta” sem sugestão. Havendo política, a necessidade é `máximo(0, alvo − posição)`; aplica-se a quantidade mínima e depois arredondamento para cima pelo múltiplo. Todas as quantidades permanecem na unidade cadastral do material; nenhuma conversão implícita é feita.

## Entregue e limites

A migration forward-only cria política com PK bigint identity, constraints, índice, permissões e pós-condição. A edição verifica contexto, fornecedor ativo do mesmo tenant/entidade, concorrência por versão e auditoria na mesma transação. A lista usa autorização persistente, paginação no servidor, ordenação estável, filtros, contadores do conjunto completo e explicação por linha.

Não foram implementados geração automática de pedido, aprovação, inventário físico, bloqueio/quarentena nem previsão de compras, pois os dois catálogos de compras ainda não têm adaptador canônico de material/almoxarifado. Próximo item exato: criar uma RC para vincular de forma persistente material bigint à requisição canônica escolhida, fotografar política/análise, revalidar sob lock e garantir idempotência/concor­rência do encaminhamento antes de expor a confirmação.
