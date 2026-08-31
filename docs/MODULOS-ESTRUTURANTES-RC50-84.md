# Módulos Estruturantes — RC50.84

## Entrega

A RC50.84 retoma a fundação compartilhada de Governança, Protocolo/Ouvidoria/SIC,
Compras/Licitações/Contratos, Financeiro/Orçamento/Contabilidade,
Tributos/Arrecadação/Dívida e RH/Folha/Portal do Servidor.

A migration `20260831210000` acrescenta onze registros de fluxo e histórico,
todos segregados por tenant e entidade e qualificados por esfera, órgão, unidades
e exercício. Ela é reexecutável, não remove objetos e não cria dados operacionais.

## Contratos de negócio

1. Permissões são persistidas e avaliadas em modo *fail-closed*.
2. Fluxos mantêm prioridade, prazo, status e trilha temporal.
3. Valores são não negativos; percentuais ficam entre zero e cem.
4. Término não pode anteceder início.
5. Integrações referenciam registros reais por `referencia_id`; a indisponibilidade
   de schema não é convertida em sucesso artificial.
6. Informações de servidor, contribuinte e interessado permanecem nos cadastros
   mestres existentes, evitando duplicidade e reduzindo exposição LGPD.

## Superfícies e relatórios

As rotas estruturantes existentes permanecem como portas de entrada MVC/Razor.
Filtros devem carregar esfera, órgão, unidade e exercício a partir do contexto
autorizado. CSV deve respeitar esses filtros, mascarar dados protegidos e neutralizar
valores iniciados por `=`, `+`, `-`, `@`, tabulação ou retorno de carro.

## Operação

Aplicar migrations exclusivamente pela ordem do manifesto. Antes da publicação,
validar checksum, executar o build .NET 10, validar PostgreSQL 16+ e fazer smoke
das rotas com usuário que possua as permissões específicas do módulo.
