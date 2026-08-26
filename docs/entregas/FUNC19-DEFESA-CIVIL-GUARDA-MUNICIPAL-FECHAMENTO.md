# Fechamento FUNC19

## Entrega funcional

Foi implementado o módulo persistente de Defesa Civil e Guarda Municipal, com quinze tabelas, RBAC, isolamento tenant/entidade, auditoria transacional, dashboard, cadastros e operações Razor responsivas, menu e nove relatórios CSV protegidos contra injection.

## Critérios verificados

- Relacionamentos são selects por rótulo ou checkboxes; não há entrada manual de IDs.
- POSTs de salvar/excluir têm antiforgery; formulário inválido recompõe opções do banco.
- Dashboard e CSV usam queries Dapper reais e nunca números fixos ou dados simulados.
- Constraints e validações cobrem datas, coordenadas, capacidades, quantidades, encerramentos e planos ativos.
- Migration, manifest e seis scripts consolidados foram sincronizados.

## Comandos e bloqueios

Executados: `jq empty`, `sha256sum`, buscas estáticas com `rg` e verificações de sincronismo. `dotnet restore`, `dotnet build`, aplicação via `psql` e smoke do servidor estão **BLOCKED**: os executáveis `dotnet` e `psql` não existem neste ambiente. Nenhum resultado foi simulado.

## Revisão CORR19

- As listagens agora oferecem detalhes, edição e exclusão lógica confirmada; criação e edição compartilham formulário responsivo com `ModelState` preservado.
- O repositório aceita somente recursos e colunas da configuração fechada, converte datas/números/booleanos explicitamente, confere relações no tenant/entidade e audita criação, alteração e exclusão.
- Status, catálogos, placa, datas, capacidades e regras de encerramento são revalidados no servidor, inclusive contra request manipulado.
- A migration corretiva `20260825131000_corr19_defesa_indices.sql`, seu checksum no manifest e todos os seis scripts consolidados foram sincronizados.
