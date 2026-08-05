# SIGOV+ RC40 — redesign premium da interface e evolução funcional do workspace

## Entrega

- App shell redesenhado com topbar premium, sidebar responsiva/offcanvas, modo compacto, filtro de menu e favoritos locais.
- Design system ampliado com tokens semânticos, componentes premium, skeletons, cards, pills, action cards, modal e estados vazios.
- Minha Central evoluída para home operacional com hero, KPIs inteligentes, Meu dia, continuidade por recentes locais e saúde do ambiente para perfis administrativos.
- Dashboard executivo sem emojis como ícones principais, com filtros de contexto, KPIs, barras CSS, donut CSS simples, insights e catálogo modular com SVG.
- Login premium com tela dividida, lembrar login, mostrar/ocultar senha e rota inicial de recuperação de senha com mensagem genérica e auditoria sem revelar existência de usuário.
- Quick Create funcional e acessível via modal acionado por `data-sigov-quick-create`.
- Paleta Ctrl+K com endpoint `/Busca/Sugestoes?q=`, debounce, fallback seguro e resultados categorizados.
- Centro de notificações visual no sino, toasts e alertas mantendo padrões acessíveis.

## Checklist de validação manual

- [ ] `/Auth/Login` exibe tela premium sem overflow no mobile.
- [ ] `/Auth/EsqueciMinhaSenha` registra solicitação com mensagem genérica.
- [ ] `/MinhaCentral` mostra hero, KPIs, Meu dia, recentes e quick create.
- [ ] `/Dashboard` mostra KPIs, filtros, gráficos CSS e insights sem emojis.
- [ ] `/Busca/Sugestoes?q=ged` retorna JSON seguro com limite de resultados.
- [ ] Botão **Novo** abre e fecha modal com ESC e mantém foco dentro do modal.
- [ ] Ctrl+K abre a paleta e retorna fallback quando backend estiver indisponível.
- [ ] Sidebar mobile abre/fecha e desktop alterna modo compacto persistido no localStorage.
- [ ] Tema claro/escuro alterna e persiste no localStorage.
- [ ] Links principais apontam para rotas MVC existentes ou hubs/áreas já tratadas.

## Evidência técnica

Validar com `dotnet build SIGOV-PLUS.sln` ou a solução disponível no ambiente. Se o banco local não estiver completo, os serviços usam fallback seguro e registram logs com CorrelationId.
