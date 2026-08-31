# Entrega RC50.86 — Financeiro, orçamento e contabilidade

## Entregue

- migration idempotente com 38 tabelas canônicas multi-esfera e PK `bigint identity`;
- contexto obrigatório de tenant, entidade, exercício e esfera, com dimensões de órgão e unidades;
- integridade de planejamento, execução orçamentária, receita, tesouraria, conciliação, restos a pagar e contabilidade;
- permissões institucionais solicitadas, mantidas no banco;
- sincronização dos quatro scripts completos e do manifesto com SHA-256;
- documentação dos limites das integrações externas.

## Revisão e limites

Foi revisado o núcleo financeiro existente em Application, Infrastructure, Api e Web. Não foram introduzidos EF Core, mocks ou dados fictícios. Esta entrega não afirma integração externa nem substitui destrutivamente o modelo legado. A compilação ficou bloqueada no ambiente por ausência do SDK `dotnet` no PATH. Não houve conflito Git porque o checkout não possui `origin`.

## Comandos

Foram executados `git status --short --branch`, `git branch --show-current`, `git remote -v`, `dotnet build`, pesquisas com `rg`, geração/verificação SHA-256 e validações estáticas de SQL/JSON.
