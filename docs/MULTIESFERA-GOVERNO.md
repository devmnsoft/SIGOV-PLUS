# Governo multi-esfera

O SIGOV PLUS adota `municipal`, `estadual` e `federal` como valores canônicos de
`esfera_governo`. A esfera contextualiza a regra; ela não substitui tenant,
entidade, órgão, unidade gestora, unidade executora ou exercício.

## Modelo institucional

- **Municipal:** prefeitura, câmara, secretaria, autarquia, fundação e unidade executora.
- **Estadual:** governo, secretaria estadual, regional, hospital, escola, autarquia,
  fundação e empresa pública.
- **Federal:** ministério, autarquia federal, fundação, instituto, superintendência,
  unidade descentralizada e programa federal.

Toda operação persistida deve preservar `tenant_id` e `entidade_id` e, quando
aplicável, órgão, unidade e exercício. Jurisdição, UF, município e região são
atributos de abrangência, não mecanismos de autorização.

## Regras de implementação

Parâmetros legais e operacionais são resolvidos no banco por esfera e contexto.
Ausência de parâmetro ou permissão falha explicitamente. Interfaces não solicitam
IDs técnicos: seleções são provenientes de cadastros autorizados. Consultas e
exportações aplicam os mesmos filtros e mascaramento LGPD da visualização.

As tabelas complementares da RC50.84 registram histórico e fluxo sem substituir
os cadastros transacionais existentes. Os vínculos de contexto têm FKs reais e
índices compostos; checks impedem esfera, status, percentual, valor ou intervalo
de datas inválido.
