# RC50.57-FUNCIONAL-PROD — relatório final

Data: 2026-08-19. Decisão: **não apto até gate runtime verde**.

1. **Módulos cadastrados:** 50 itens no catálogo existente, agora apresentados com acesso efetivo.
2. **Funcionais:** núcleos operacionais de Segurança, Processos/GED, Tributário, Financeiro, Educação, Saúde, Saneamento, RH, Compras e operação já persistem fluxos conforme RCs anteriores; falta revalidação runtime.
3. **Parciais:** LGPD/Auditoria, integrações, relatórios e módulos avançados com cobertura heterogênea.
4. **Pendentes:** itens Beta/Em implantação e integrações oficiais não homologadas.
5. **Perfis:** 11 perfis funcionais consolidados na matriz.
6. **Importância:** segregam plataforma, tenant, decisão, coordenação, operação, finanças, controle, atendimento, gestão modular, leitura e autosserviço cidadão.
7. **Matriz:** tela com módulo/recurso/ação/estado/motivo e exportação protegida.
8. **SuperAdmin:** vê/acessa todo catálogo.
9. **AdminTenant:** depende de módulo habilitado/permissão e permanece no tenant.
10. **Coordenador:** concessões da área; configuração global bloqueada.
11. **Financeiro:** escopo financeiro; estorno e sensíveis exigem grant específico.
12. **Auditor:** leitura de trilhas/LGPD/relatórios.
13. **Atendimento:** protocolo/ouvidoria/e-SIC sem baixa financeira.
14. **Menus:** Catálogo/Meu Acesso e governança sensível dinâmicos.
15. **Backend:** detalhe modular retorna 403 e audita negativa; 401 é responsabilidade do `[Authorize]`.
16. **Proteção:** módulo exige grant ou bypass SuperAdmin.
17. **Funcionalidades corrigidas:** catálogo sem botão morto de “solicitar”; Meu Acesso, detalhe e CSV têm ações reais.
18. **Regras:** acesso, exportação e bloqueios explicitados; regras profundas legadas permanecem para homologação.
19. **Integrações implementadas:** catálogo↔permissão↔menu↔auditoria.
20. **Preparatórias:** Folha/Compras/Saneamento/Tributário↔Financeiro e Almoxarifado↔Patrimônio aguardam prova E2E.
21. **Segurança:** frontend reflete, backend decide; padrão é negar.
22. **LGPD:** aviso em cards sensíveis e motivo de bloqueio explícito.
23. **Auditoria:** negativa modular e exportação permitida/negada registram contexto operacional.
24. **501:** nenhum padrão essencial estático encontrado.
25. **404:** novas rotas possuem actions reais; conjunto completo requer smoke.
26. **500:** sem correção alegada sem execução runtime.
27. **Banco:** não executado neste host: `psql` ausente; bloqueio ambiental, não aprovação.
28. **Build:** não executado neste host: `dotnet` ausente; bloqueio ambiental, não aprovação.
29. **Gate:** validadores estáticos passaram com 49/126/7 avisos históricos; smoke runtime e Windows permanecem bloqueados pelas ferramentas/host ausentes.
30. **RC50.58:** ampliar menu descritor, enforcement em controllers legados e homologar cada CRUD/regra/integração com banco real.

Nenhum teste, mock, fixture ou projeto de teste foi criado.
