# RC50.66 — relatório Agro/Geo integrado a todos os módulos

Data: 2026-08-19. Decisão: **não apto para produção até banco, build, gate e jornadas autenticadas ficarem verdes**. Entrega aditiva, sem classe/projeto de teste e sem migration desnecessária.

1. **Módulos preservados:** os 15 grupos do inventário (governança, segurança, arrecadação, sociais, documentais, administrativos, empresariais e Agro/Geo); nenhum arquivo funcional foi removido.
2. **Módulos impactados:** Agro/Geo, catálogo SaaS e shell/menu. Demais famílias não tiveram código alterado.
3. **Continuidade:** controllers, services, repositories, views, scripts, migrations, permissões e menus preexistentes permanecem no tree. Continuidade runtime aguarda ambiente.
4. **Agro:** dashboard, produtor, propriedade, talhão, cultura, safra, produção, programas, benefícios, insumos, patrulha, relatórios e transparência permanecem disponíveis conforme autorização API.
5. **Geo:** mapa, camadas, feições Point/LineString/Polygon/MultiPolygon/GeoJSON, validação de coordenadas/geometria e exportação tenant-scoped existentes.
6. **Controllers:** as superfícies Web internas `Agro`, `AgroBi`, `AgroPainelComercial`, `AgroRelatorios` e `AgroTransparencia` agora exigem autenticação. `AgroPublico` permanece público por desenho; APIs continuam `[Authorize]` + módulo + permissão no service.
7. **Services:** `AgroGeoService` passou a auditar exportação autorizada com formato e tamanho, sem conteúdo, nome pessoal ou documento no evento.
8. **Repositories:** não precisaram de alteração; a revisão confirmou Dapper, SQL parametrizado, projeções explícitas e `tenant_id`/entidade no Geo.
9. **Views:** nenhuma removida ou substituída; Mapa e Camadas existentes ganharam descoberta pelo menu.
10. **JavaScript:** `agro.crud.js` e `agro.geo.js` preservados e sintaticamente válidos; nenhuma simulação foi introduzida.
11. **Menus:** adicionados “Mapa Agro” e “Camadas geográficas”; Agro, Programas e Patrulha foram preservados.
12. **Botões:** nenhum removido. Formulários Geo continuam ligados aos endpoints reais de camadas/feições.
13. **Produtor:** cadastro e máscara permanecem sob services/repositories e grants existentes; homologação de PII por perfil é P0.
14. **Propriedade:** vínculo ao produtor e dados geográficos persistentes preservados.
15. **Talhão:** vínculo obrigatório à propriedade e geometria preservados.
16. **Culturas/safras/produção:** cadastros e restrições de valores não negativos preservados.
17. **Programas/benefícios:** fluxos persistentes e permissões granulares preservados.
18. **Insumos:** estoque/distribuição preservados; prova concorrente de saldo é pendência de homologação.
19. **Patrulha:** máquinas, implementos, agenda e serviços preservados; regras de status continuam no service.
20. **Mapa/camadas/GeoJSON:** exportação projeta somente nome/tipo/geometria, filtra tenant/entidade, exige `agro.geo.exportar` e audita sucesso.
21. **Financeiro:** estruturas preparatórias existentes não foram duplicadas; jornada transacional Agro→Financeiro permanece P1 real.
22. **Processos/GED:** vínculos/documentos rurais existentes foram preservados; anexação ponta a ponta permanece P1.
23. **Transparência:** painel público separado foi preservado; nenhum dado pessoal foi adicionado ao GeoJSON público.
24. **Pendências/Alertas/Qualidade:** núcleo RC50.63 aceita Agro; producers contínuos com dados reais permanecem P1, sem preencher centrais com ficção.
25. **Auditoria:** criar/alterar/excluir camadas/feições já auditava; exportação GeoJSON foi fechada nesta RC.
26. **LGPD:** payload de auditoria registra apenas formato/tamanho; exportação não consulta pessoa/documento/telefone/e-mail/endereço.
27. **Não regressão:** checklist e inventário criados; nenhuma ocultação, controller comentado, migration removida ou `OperationalDemoService` introduzido.
28. **501:** busca encontrou somente SQLSTATE PostgreSQL `42501` em diagnóstico; não há HTTP 501/NotImplemented no escopo pesquisado.
29. **Botões mortos:** nenhum novo achado estático no eixo tocado; confirmação global depende de clique autenticado.
30. **Dashboards:** nenhum código de dashboard foi removido; ausência de 500 não é alegada sem runtime.
31. **Banco:** bloqueado: `psql` ausente (exit 127). Nenhuma migration foi criada porque o schema Agro/Geo e transversal requerido já existe.
32. **Build:** clean/restore/build bloqueados: `dotnet` ausente (exit 127).
33. **Gate:** passos estáticos passaram; smoke bloqueou corretamente (exit 2) por ausência de `psql`, `pg_dump`, `pg_restore` e .NET. PowerShell bloqueado (exit 127). Os validadores mantêm avisos históricos conservadores.
34. **RC50.67:** executar banco limpo/parcial, build e jornadas multi-tenant; cadastrar/homologar templates explícitos Técnico Rural/Operador Patrulha/Atendimento Rural sobre grants existentes; implementar producers idempotentes Agro para centrais; fechar anexos GED e obrigação financeira sem duplicação; homologar todos os botões/exportações/perfis.

## Evidências estáticas

- Inventário amplo: 11.217 linhas; navegação/actions/fetch: 524 linhas.
- Manifest válido; validadores de índices concluídos com avisos históricos; 611 rotas API sem conflito direto.
- Zero projeção curingas, raw strings C# 11 e `.TotalCount` nas buscas obrigatórias.
- `node --check` passou nos dois scripts Agro exigidos.
- Nenhum arquivo em `tests/**`, `*Tests.cs`, mock ou fixture foi criado.
