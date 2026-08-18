# RC50.50 — Saneamento Avançado

## Entrega
A sprint evolui a base já existente sem duplicar os fluxos legados. Quatro migrations incrementais e idempotentes cobrem gestão comercial, leitura/faturamento/arrecadação, operação de campo e GIS/laboratório/qualidade. As estruturas usam tenant, soft delete, auditoria JSON, CorrelationId e índices somente sobre colunas materializadas.

A camada funcional Dapper usa lista branca de tabelas, SQL parametrizado e preflight por `IDatabaseObjectInspector`. A API oferece dashboards, consultas, cadastros, transições auditadas e CSV sem documento, telefone, e-mail, endereço ou geolocalização. Regras mínimas impedem consumidor sem nome, leitura incompleta, coordenadas inválidas, OS sem descrição, conclusão sem desfecho e cancelamento/reprovação sem justificativa. Banco/PIX, PostGIS, mapas, telemetria, hidrômetro inteligente e laboratório externo permanecem **preparatórios**.

## Banco
- Comercial: consumidor/contato/imóvel, ligação e históricos, hidrômetro e históricos, categoria, tarifa, atendimento e eventos.
- Faturamento: rotas/itens, leituras/ocorrências, lotes, faturas/itens, guia preparatória, pagamentos, inadimplência, parcelamentos e eventos.
- Campo: equipes, OS/movimentos/evidências, corte, religação, vazamento, manutenção, vistoria e eventos.
- Cadastro técnico e qualidade: unidades, pontos GIS numéricos, redes/trechos, equipamentos, parâmetros, coleta, amostras, ensaios, alertas e eventos.

## API e telas
Os controllers `/api/saneamento/comercial`, `/faturamento`, `/operacao` e `/gis-qualidade` expõem recursos reais isolados por tenant. As páginas correspondentes usam o mesmo design system institucional: hero, KPIs, filtros, tabela responsiva, status, badge LGPD, loading, empty state, exportação e rastreabilidade. O dashboard principal não usa `OperationalDemoService`.

## Validação e pendências
Manifest e scripts consolidados foram atualizados com checksums. Validadores estáticos, conflito de rotas e busca de padrões proibidos devem ser executados no fechamento. PostgreSQL, .NET, Swagger e login só podem ser declarados aprovados após execução real; neste ambiente os binários não estavam disponíveis e a tentativa de instalar PostgreSQL foi bloqueada pelo proxy APT. RC50.51 deve homologar permissões granulares por ação, clientes externos preparatórios, storage protegido de evidências e transações integradas entre domínios.
