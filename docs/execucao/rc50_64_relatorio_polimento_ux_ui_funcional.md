# RC50.64 — relatório de polimento UX/UI funcional

Data: 2026-08-19. Decisão: **não apto para produção até banco, build, gate e jornadas autenticadas ficarem verdes**. A entrega não cria módulo, migration, teste, mock ou dado fictício.

1. **Telas revisadas:** shell, Minha Central, estados compartilhados e páginas de erro; dashboards/formulários foram inventariados para homologação incremental.
2. **Menus:** sidebar móvel já era colapsável e porções sensíveis já usam `IMenuPermissionService`; fechamento por `Escape` foi adicionado. A conversão integral dos grupos estáticos permanece P0.
3. **Dashboards:** rotas críticas foram inventariadas; nenhuma ausência de 500 é alegada sem runtime.
4. **Minha Central:** removida ação sem persistência; fallback só aparece em degradação real; tenant ausente não é chamado de demonstração; papéis administrativos reconhecem os aliases reais.
5. **Cards:** matriz, ACS e atendimento passaram a usar rotas reais; perfil genérico recebe somente “Meu acesso”; vazio de atalhos explica ausência de grant.
6. **Botões:** o botão local “Marcar como visto”, que não persistia nem auditava, foi substituído pelo link real “Ver todas”.
7. **Formulários:** criado resumo acessível reutilizável, com foco automático; a adoção formulário a formulário permanece P1.
8. **Estados vazios:** padronizados acesso negado, estrutura pendente, módulo bloqueado, falha e ausência de atalhos, sem simulação de dados.
9. **403/404/500:** mensagens agora são distintas e orientadas à ação; 500 mostra apenas correlation id e jamais exceção/SQL/segredo.
10. **Responsividade:** ações e estado de sistema empilham no celular; tabelas recebem rolagem horizontal controlada e o conteúdo principal não força overflow.
11. **Acessibilidade:** skip link/foco visível existentes foram preservados; `Escape` fecha sidebar; erro usa `role`, rótulo e heading; movimento reduzido é respeitado.
12. **Ícones:** estados usam vocabulário existente (`locked`, `info`, `warning`, `search`) por semântica.
13. **Textos:** removida linguagem de “ambiente demonstração” da Central; mensagens técnicas genéricas deram lugar a instruções institucionais.
14. **Permissões na UI:** cards recomendados continuam derivados de perfil e o acesso genérico aponta ao catálogo calculado pelo backend; menus sensíveis existentes permanecem condicionados.
15. **Backend:** `[Authorize]`, políticas, serviço de acesso modular e status 403 continuam autoridades; nenhuma proteção foi substituída por ocultação visual.
16. **Exportações:** nenhuma lógica foi afrouxada; a homologação de grants, máscara, filtros, limite e auditoria em exportadores legados segue P0.
17. **LGPD:** nenhuma PII nova é renderizada; correlation id não inclui conteúdo pessoal; consultas da Central continuam tenant-scoped e com projeções explícitas.
18. **Auditoria:** logs de falha/erro existentes foram preservados; não se alega auditoria para interação apenas local.
19. **Rotas 404:** três links de recomendação foram corrigidos; a ausência global de 404 depende do smoke autenticado.
20. **Dashboards 500:** fallback sanitizado foi reforçado, mas a ausência de 500 depende de banco/runtime.
21. **Endpoints 501:** busca estática não localizou endpoint essencial 501.
22. **Banco:** nenhum schema novo foi necessário; registrar abaixo o resultado real do apply obrigatório.
23. **Build:** registrar abaixo o resultado real do clean/restore/build com warnings como erros.
24. **Gate:** registrar abaixo o resultado real do smoke; bloqueio ambiental não equivale a aprovação. PowerShell deve ser executado em host Windows.
25. **RC50.65:** concluir menu integralmente dirigido por grants, aplicar componentes aos CRUDs legados, homologar exports e executar matriz de jornadas com massa segregada.

## Evidências de validação

Os resultados finais dos comandos obrigatórios são registrados no fechamento/PR. Falha de ferramenta, serviço ou rede permanece bloqueio explícito. Nenhuma classe em `tests/**` ou `*Tests.cs` foi criada.

### Resultado registrado neste host

- Manifest JSON: válido.
- Validadores de índices: concluídos com sucesso e avisos conservadores históricos (incluindo 129 avisos do verificador geral e 7 de imutabilidade).
- Conflitos de API: nenhum conflito direto em 611 rotas.
- Projeções `SELECT *`, raw strings C# 11 e `.TotalCount`: nenhuma ocorrência nas buscas obrigatórias.
- Busca 501: única ocorrência é o código PostgreSQL `42501` na tela de diagnóstico, não HTTP 501 nem implementação ausente.
- Banco, clean, restore e build: bloqueados porque `psql` e `dotnet` não estão instalados (exit 127).
- Smoke: etapas estáticas passaram; gate bloqueado corretamente (exit 2) por ausência de `psql`, `pg_dump`, `pg_restore` e .NET.
- Gate Windows: bloqueado porque `pwsh` não está instalado (exit 127).
