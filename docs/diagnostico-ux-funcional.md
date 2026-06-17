# Diagnóstico UX e Funcional — SIGOV PLUS

Auditoria realizada em 2026-06-17 sobre `Program.cs`, layout/sidebar, `OperationalDemoService`, controllers MVC, assets CSS/JS e views principais de Dashboard, Login, Manual, Módulos, SaaS, Segurança, Auditoria, Health, Implantação e módulos operacionais.

| Tela/rota | Status | Problema encontrado | Correção proposta | Prioridade |
|---|---|---|---|---|
| `/Auth/Login` | funcional | Autenticação existe, mas pós-login levava direto ao dashboard e não à jornada por perfil. | Redirecionar administradores/operadores para `/MinhaCentral` e manter fallback seguro. | Alta |
| `/Dashboard` | parcial | Dashboard premium existe, porém era ponto inicial único e não explicava a jornada pessoal. | Manter como visão executiva e criar central de trabalho por perfil. | Alta |
| `/MinhaCentral` | quebrada/inexistente | Item obrigatório da jornada não existia. | Criar controller e view com saudação, contexto, pendências, atalhos, LGPD e health. | Alta |
| Sidebar/menu | parcial | Grupos não seguiam integralmente a jornada solicitada e alguns itens usavam fallback genérico sem deixar claro. | Reorganizar em Início, Implantação, SaaS, Segurança, Governo, Empresas, Operação e Ajuda; apontar itens sem tela para `/Modulo/EmImplantacao`. | Alta |
| `/Manual` | parcial | Manual por perfil existe, mas precisa evoluir em busca e conteúdos detalhados por rotina. | Manter accordion por perfis e expandir artigos nas próximas iterações. | Média |
| `/Saas/Tenants`, `/Saas/Modulos` | parcial | Consultas Dapper têm fallback amigável, mas criação/edição ainda depende de implementação completa. | Completar CRUD com auditoria, validação e toasts. | Alta |
| `/Saas/Planos`, `/Saas/Parametros`, `/Saas/Implantacao` | parcial/mock | Telas demonstrativas/visuais com fluxo ainda incompleto. | Persistir planos, parâmetros por escopo e checklist guiado em tabelas reais. | Alta |
| `/Seguranca/Usuarios` | parcial/mock | Controller entrega view/form, mas CRUD real de listar/criar/editar/inativar/reset visual ainda é limitado. | Implementar serviço Dapper com try/catch + ILogger, mascaramento e auditoria. | Alta |
| `/Seguranca/Perfis`, `/Seguranca/Permissoes` | parcial/mock | Matriz visual existe, permissões por módulo ainda não persistem ponta a ponta. | Persistir perfil, permissões, ativação/inativação e histórico. | Alta |
| `/Auditoria/Trilhas`, `/Auditoria/AcessosDadosPessoais` | parcial | Telas de auditoria existem, precisam integração mais profunda com todas as ações críticas. | Padronizar chamadas de auditoria em cadastros e ações de módulo. | Alta |
| `/Operacao/Health` | parcial | Health visual existe; validação Docker/API deve confirmar integração real. | Exibir Web, API, PostgreSQL, migrations, versão e última verificação. | Alta |
| `/Modulo/EmImplantacao?codigo=...` | funcional | Fallback evita 404 para módulos sem backend. | Usar como destino oficial para itens de roadmap. | Alta |
| Módulos operacionais | parcial/mock | `OperationalDemoService` fornece dashboard/listagem/detalhe demonstrável para vários módulos; alguns backends reais ainda ausentes. | Manter fallback visual explícito e implementar CRUD real por domínio prioritário. | Média |
| CSS/layout | parcial | Já há tema, cards e grids; faltava assistente contextual e alguns componentes premium. | Adicionar assistente flutuante, hero e reforços visuais. | Média |
| JS global | parcial/conflito | `sigov.ui.js` e `sigov-ui.js` coexistiam; risco de duplicidade/confusão. | Remover carregamento duplicado antigo e centralizar APIs globais em `sigov-ui.js`. | Alta |
| Pop-ups/onboarding | parcial | Toast/confirm/help existem; primeiro acesso era limitado. | Implementar `SigovOnboarding.firstAccess` para telas principais e localStorage. | Alta |
| Service worker/assets | não validado | Necessário validar em navegador/Docker para console 404/cache. | Não registrar SW em localhost e revisar manifesto/favicons na próxima evolução. | Média |

## Observações gerais

- O repositório já possui estrutura ampla MVC/Razor, módulos por domínio, Dapper, auditoria e fallbacks.
- O principal gap era a jornada: muitos recursos existiam, mas a entrada do usuário não orientava próximos passos.
- A navegação precisava garantir que itens não implementados caíssem em uma página de implantação, não em 404.
- Cadastros críticos ainda requerem uma rodada dedicada de persistência ponta a ponta com tabelas reais e auditoria completa.
