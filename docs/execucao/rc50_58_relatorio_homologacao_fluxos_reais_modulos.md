# RC50.58 — relatório de homologação dos fluxos reais

Data: 2026-08-19. **Decisão: não apto até apply, build e smoke runtime verdes.** Este relatório não promove inventário estático a homologação.

1. **Fluxos homologados:** nenhum foi declarado ponta a ponta sem banco/runtime.
2. **Fluxos corrigidos:** cancelamento na API canônica de Processos Digitais passou a receber justificativa obrigatória.
3. **Fluxos parciais:** todos os 17 grupos inventariados possuem superfícies, com maturidade descrita na matriz.
4. **Regras corrigidas:** Processos rejeita justificativa vazia e cancelamento de processo inexistente, encerrado ou já cancelado.
5. **Integrações implementadas:** catálogo/permissão/menu/auditoria; protocolo↔processo; núcleos financeiros/outbox já existentes.
6. **Integrações preparatórias:** e-SUS/SISAB, Folha→Financeiro, medições/recebimentos→Financeiro/Patrimônio e provedores oficiais.
7. **Perfis validados estaticamente:** os 11 perfis da RC50.57 permanecem na matriz; validação autenticada está pendente.
8. **Módulos por perfil:** concessão combina tenant, módulo contratado/habilitado e permissão; SuperAdmin tem bypass.
9. **Permissões corrigidas:** cancelamento de processo continua exigindo `processos.processo.cancelar` no backend.
10. **Menus corrigidos:** nenhum novo ajuste; catálogo/Meu Acesso e governança dinâmica vêm da RC50.57.
11. **Botões corrigidos:** nenhum botão removido ou ocultado para disfarçar pendência.
12. **Endpoints 501:** busca estática não encontrou endpoint essencial 501.
13. **Dashboards 500:** sem alegação de correção sem runtime.
14. **Menus 404:** sem alegação de correção sem smoke autenticado.
15. **Segurança:** validação ocorre antes da mutação; estado terminal não pode ser cancelado novamente.
16. **LGPD:** justificativa fica na trilha de auditoria, sem incluir documento pessoal.
17. **Auditoria:** cancelamento registra status anterior, novo status e justificativa normalizada.
18. **Banco:** apply não executado porque `psql` não está instalado neste host (`exit 127`); P0 ambiental permanece.
19. **Build:** clean/restore/build não executaram porque `dotnet` não está instalado (`exit 127`); não há aprovação de compilação.
20. **Gate:** manifest, três validadores de índices e análise de 605 rotas passaram; permanecem 49/126/7 avisos conservadores históricos. O smoke terminou bloqueado (`exit 2`) por ausência de `psql`, `pg_dump`, `pg_restore` e .NET. O gate Windows não executou porque `pwsh` não está instalado.
21. **RC50.59:** executar roteiros autenticados com massa segregada por tenant; fechar exportadores legados, transições e integrações que falharem; não iniciar módulos novos.

Nenhuma classe de teste, fixture, mock ou projeto de teste foi criado.
