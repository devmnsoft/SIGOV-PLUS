# Padrão de exportação CSV do SIGOV PLUS

## Contrato MVC/Razor

- A view deve gerar a rota com `Url.Action`, tag helpers (`asp-controller`, `asp-action` e `asp-route-*`) ou interpolação Razor integral. Nunca acrescente `.csv` diretamente depois de uma expressão Razor.
- O link deve apontar para uma action existente, protegida por autenticação e pela permissão específica de exportação.
- A action retorna `File` com `text/csv; charset=utf-8` e nome terminado em `.csv`.
- Filtros visíveis devem ser propagados à exportação. A consulta deve aplicar, conforme o módulo, `tenant_id`, `entidade_id`, `exercicio_id`, esfera e demais dimensões autorizadas.
- Ausência de permissão, contexto, schema ou plano deve falhar explicitamente; não é permitido produzir dado fictício nem simular sucesso.

## Segurança do conteúdo

Todo valor textual deve ser serializado por uma função CSV única que:

1. neutralize os prefixos de fórmula `=`, `+`, `-`, `@`, tabulação e retorno de carro, inclusive após espaços iniciais;
2. duplique aspas internas e envolva o campo em aspas quando houver delimitador, aspas ou quebra de linha;
3. não registre CPF, CNPJ, token, senha nem o conteúdo sensível exportado;
4. use UTF-8 e mantenha cabeçalho estável;
5. registre somente metadados seguros na auditoria (operação, usuário/contexto, recurso, horário e correlação).

A neutralização ocorre na camada que compõe o arquivo, antes da codificação. Validação no navegador não substitui essa proteção.

## Experiência e mensagens

A página deve conter o mini manual **Como usar esta tela**, explicar filtros, permissão, sensibilidade e resultado. Estados esperados devem ser claros: relatório gerado, nenhum dado encontrado, filtro obrigatório ausente, usuário sem permissão, erro de geração e bloqueio pelo plano SaaS. Downloads não devem expor identificadores técnicos na interface.

## Checklist de revisão

- rota resolvida por action real e parâmetros com os mesmos nomes da action;
- autorização e contexto fail-closed;
- filtros parametrizados, sem SQL concatenado nem `SELECT *`;
- `Content-Type` e filename corretos;
- proteção contra CSV injection coberta pela implementação existente;
- smoke autenticado da rota e inspeção do cabeçalho/primeira linha;
- build Razor executado com o SDK de `global.json`.
