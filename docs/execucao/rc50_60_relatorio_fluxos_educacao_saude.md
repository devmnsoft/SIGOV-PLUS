# RC50.60 — relatório dos fluxos de Educação e Saúde

Data: 2026-08-19. Decisão: **não apto para produção enquanto apply, build e smoke runtime não estiverem verdes**.

1. **Inventário Educação:** API/Web, serviços e repositórios Dapper canônicos, Bloco 3 e avançados; tabelas de escola a indicadores presentes.
2. **Inventário Saúde:** API/Web, serviços e repositórios Dapper canônicos e avançados; tabelas de unidade a retaguarda presentes.
3. **Endpoints:** a API canônica de Educação agora exige módulo habilitado; Saúde já possuía o filtro.
4. **Services:** negativas passaram a auditar recurso, ação, motivo, usuário e tenant.
5. **Repositories:** preservados SQL parametrizado e filtros de `tenant_id`/`entidade_id`; nenhuma simulação foi introduzida.
6. **Views:** inventariadas as telas, grids, forms, dashboards e modais existentes; nenhuma remoção cosmética foi usada.
7. **Menus:** continuam derivados do catálogo/permissões; a migration inclui as chaves setoriais ausentes.
8. **Negócio Educação:** cancelamento/transferência exigem justificativa; transferência exige nova turma; frequência exige turma/aluno.
9. **Permissões:** catálogo granular de Educação/Saúde e templates funcionais foram persistidos.
10. **Secretário Educação:** template gerencial criado; concessão efetiva continua por tenant.
11. **Coordenador Educação:** template de escopo autorizado criado.
12. **Diretor:** template de escola autorizada criado.
13. **Professor:** template de diário/turmas vinculadas criado.
14. **Secretário Saúde:** template gerencial criado.
15. **Coordenador Saúde:** template de unidades/equipes autorizadas criado.
16. **ACS:** visita exige ACS, alvo e desfecho; template de microárea criado.
17. **Farmácia:** dispensação exige paciente e quantidade positiva; permissão específica preservada.
18. **Regulação:** criação exige paciente/tipo/justificativa e movimentação exige status.
19. **Integrações Educação:** transporte, merenda, biblioteca e indicadores existentes permanecem persistentes; oficiais são preparatórias.
20. **Integrações Saúde:** ACS, vacinação, farmácia e regulação existentes permanecem persistentes; e-SUS/SISAB é preparatória.
21. **Auditoria:** mutações existentes e negativas novas registram trilha sem documento pessoal completo.
22. **LGPD:** máscaras já aplicadas a payload/listagens/exportações foram preservadas; dados clínicos continuam sensíveis.
23. **501:** busca estática não encontrou endpoint essencial 501 no escopo.
24. **Botões:** nenhum botão foi removido; smoke autenticado ainda é necessário para afirmar ausência total de ação morta.
25. **Dashboards:** serviços persistentes e protegidos preservados; sem runtime não se afirma ausência de 500.
26. **Banco:** não aplicado: `psql` ausente no host, bloqueio ambiental P0.
27. **Build:** não executado: `dotnet` ausente no host, bloqueio ambiental P0.
28. **Gate:** validadores estáticos e 605 rotas passaram; gate bloqueado por ausência de `psql`, `pg_dump`, `pg_restore` e .NET; PowerShell também ausente.
29. **RC50.61:** executar apply limpo/parcial, build e jornadas autenticadas; provar escopos escola/turma/unidade/microárea, concorrência e integrações preparatórias.

A migration RC50.60 adiciona integridade concorrente para matrícula corrente e frequência diária. Nenhuma classe de teste, mock, fixture ou projeto de teste foi criado.
