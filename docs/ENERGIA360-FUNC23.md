# Energia360 — FUNC23

## Visão funcional

O Energia360 centraliza unidades consumidoras, medidores e leituras, faturas, contratos/demanda, iluminação pública, geração distribuída, créditos, eficiência e alertas. As rotas partem de `/Energia`; todas exigem usuário autenticado e permissão `ENERGIA_*` persistida.

## Arquitetura e integrações

A aplicação MVC/Razor usa `IEnergiaRepository` e Dapper/Npgsql. Toda consulta recebe `tenant_id` e `entidade_id` dos claims, nunca da tela. Concessionárias usam `sigov.pessoa_juridica`, unidades administrativas usam `sigov.unidade_organizacional` e contratos usam `sigov.contrato`; ausências de catálogo ou schema geram falha explícita. Vínculos de protocolo, financeiro, ativos e Carbono360 guardam somente referências oficiais, sem duplicar entidades e sem simular resultados. Emissões evitadas somente são exibidas quando persistidas com fator oficial em `energia_integracao_carbono`.

## Regras e anomalias

Valores, consumo, demanda, potência, créditos e economia são não negativos. UC é única por entidade, concessionária e instalação; fatura é única por UC/competência. Coordenadas respeitam latitude `[-90,90]` e longitude `[-180,180]`. Estados excepcionais exigem justificativa no servidor.

Alertas não usam IA ou previsão. A regra transparente recomendada para `CONSUMO_ACIMA_MEDIA` compara a competência com a média aritmética das últimas 12 competências completas da mesma UC; `VARIACAO_BRUSCA_CUSTO` compara a variação percentual com o limiar cadastrado. A geração do alerta deve registrar média, amostra, limiar e valor observado em indicador persistido.

## CSV e segurança

Exportações aplicam os filtros e o contexto da tela e prefixam valores iniciados por `=`, `+`, `-`, `@`, tab ou retorno, mitigando CSV injection. Formulários usam AntiForgeryToken, validação server-side e listas provenientes do banco, sem campos de ID manual.
