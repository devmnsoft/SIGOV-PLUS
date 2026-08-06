# Checklist de homologação — RC43

## Escopo desta entrega

Esta iteração inaugura o Design System interno em `/DesignSystem`, com acesso restrito a `ADMIN_GERAL` fora de Development, componentes Razor reutilizáveis, demonstrações responsivas, estados de foco, tema e densidade. Os demais módulos da RC43 permanecem no roadmap e não devem ser considerados homologados por este documento.

## Verificações automatizadas

| Verificação | Resultado | Evidência |
|---|---|---|
| Compilação de `Sigov.Web` | Bloqueada pelo ambiente | O SDK `dotnet` não está instalado no container de execução. |
| JavaScript do Design System | Pendente de execução em navegador | Script isolado, sem dependências externas e carregado somente na rota. |
| Arquivos estáticos locais | Aprovado por inspeção | CSS e JS são servidos por `wwwroot`; nenhuma CDN foi adicionada. |
| Banco e migrations | Não aplicável | Esta entrega não altera esquema nem dados. |
| `script_completop.sql` | Não aplicável | Nenhuma alteração de banco foi realizada. |

## Roteiro manual obrigatório

- [ ] Em Development, abrir `/DesignSystem` sem autenticação e confirmar resposta 200.
- [ ] Fora de Development, confirmar redirecionamento para login quando anônimo.
- [ ] Fora de Development, confirmar 403 para usuário autenticado sem `ADMIN_GERAL`.
- [ ] Fora de Development, confirmar acesso para usuário com papel, perfil ou permissão `ADMIN_GERAL`.
- [ ] Navegar por teclado por botões, filtros, tabela, modal e navegação interna.
- [ ] Confirmar que o foco retorna ao acionador após fechar o modal.
- [ ] Acionar o toast e confirmar anúncio visual sem erro no console.
- [ ] Alternar tema claro/escuro e recarregar a página para validar persistência.
- [ ] Alternar densidade confortável/compacta.
- [ ] Validar larguras de 320 px, 768 px, 1024 px e 1440 px sem overflow horizontal.
- [ ] Ativar `prefers-reduced-motion` e confirmar ausência de shimmer animado.
- [ ] Verificar contraste de texto, badges, botões e swatches em ambos os temas.

## Matriz geral RC43

| Jornada | Estado nesta entrega |
|---|---|
| Login, shell, sidebar e navegação mobile | Regressão manual pendente |
| Minha Central e Dashboard | Fora do escopo desta iteração |
| Quick Create e Busca Global | Fora do escopo desta iteração |
| Protocolo, GED e Kanban | Componentes-base demonstrados; fluxos não alterados |
| Notificações e Atividades | Toast demonstrado; fluxos não alterados |
| Relatórios, Perfil e Preferências | Fora do escopo desta iteração |
| White label e Implantação SaaS | Fora do escopo desta iteração |
| Segurança, LGPD e Auditoria | Controle de acesso do Design System implementado |
| Operação/Health | Fora do escopo desta iteração |

## Limitações conhecidas e próximos passos

1. Executar build e suíte de testes em agente com .NET SDK compatível com o repositório.
2. Automatizar os cenários de autorização da rota com `WebApplicationFactory`.
3. Migrar progressivamente as telas operacionais para `_UiButton`, `_FilterBar`, `_DataTableToolbar` e `_Timeline`.
4. Integrar tokens white label por tenant ao provider já existente antes de alterar persistência.
5. Capturar evidências visuais desktop e mobile após disponibilizar runtime e credenciais de homologação.
