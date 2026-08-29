# Saneamento360 / SIGCOS — FUNC13

## Arquitetura e limites

O módulo usa ASP.NET Core MVC/Razor, APIs protegidas, Dapper/Npgsql e PostgreSQL. O contexto de toda consulta e mutação inclui `tenant_id` e `entidade_id`; `exercicio_id` e `unidade_id` são propagados quando aplicáveis. Ausência de schema ou integração é erro explícito: não há catálogo, mapa, retorno bancário, PIX ou dado demonstrativo artificial.

O cadastro técnico é apresentado em tabela georreferenciada quando não existe provedor de mapas configurado. Captura móvel é responsiva/PWA-friendly, mas este ciclo não entrega aplicativo nativo/offline. Fotos, GPS, assinatura, estoque do Ativos360, receita do Financeiro e protocolos do Cidadão360 somente são usados quando a infraestrutura real correspondente estiver disponível.

## Cobertura

- Dashboard comercial e operacional: consumidores, ligações, hidrômetros, leitura, faturamento, arrecadação, inadimplência, parcelamentos, OS, equipes e qualidade.
- Pessoas do `core`, consumidores, unidades, ligações, hidrômetros, aferições e substituições, com histórico e auditoria LGPD.
- Rotas, leituras, ocorrências, críticas e revisão; tarifas por vigência/faixa e memória de cálculo.
- Faturas, itens, baixa controlada, cobrança e parcelamento sem simulação bancária.
- OS, execução, equipe e consumo de material, respeitando a movimentação real do Ativos360.
- Redes, unidades operacionais, pontos/amostras/parâmetros/resultados e alertas de não conformidade.
- CSV neutraliza células iniciadas por `=`, `+`, `-` ou `@`; exportação pessoal exige permissão própria e registra auditoria.

## Reuso de modelo

Os nomes físicos históricos `sigov.saneamento_*` são preservados. `saneamento_leitura` representa leitura de consumo, `saneamento_parcelamento_item` representa parcela e `saneamento_servico_executado` representa execução da OS. Essa decisão evita duplicação destrutiva das entidades publicadas. A migration EXP13 somente acrescenta aferição, substituição, revisão, faixa tarifária, cobrança, material e alerta que ainda não existiam.

## Segurança e regras

POSTs das telas Razor usam antiforgery e validação de modelo; seleções relacionais vêm de pesquisa/autocomplete validado, nunca de campo de ID livre. Queries Dapper são parametrizadas. Valores, consumos e quantidades negativos são rejeitados no domínio e no banco. CPF/CNPJ, quando informado no fluxo de pessoa, é validado e permanece mascarado em listas e CSV; consulta/exportação registra usuário, contexto e correlação na auditoria.
