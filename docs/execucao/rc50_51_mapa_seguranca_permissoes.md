# RC50.51 — Mapa de segurança e permissões

| Capacidade | Estado | Evidência | Risco remanescente |
|---|---|---|---|
| Usuários, grupos e perfis | Implementado | `usuario`, `usuario_grupo`, `perfil_permissao` e administração Web existente | P1: homologação por tenant |
| Permissão módulo/recurso/ação | Implementado incrementalmente | `seguranca_recurso` e `seguranca_permissao_granular` | P1: conectar todos os módulos legados ao avaliador |
| Exceção por usuário/perfil | Implementado incrementalmente | `seguranca_usuario_permissao` e `seguranca_perfil_permissao` | P1: homologar precedência negar/conceder |
| Restrição tenant/entidade | Implementado no modelo | `tenant_id`, `entidade_id`, `escopo` e `seguranca_restricao_acesso` | P1: ampliar cobertura nos controllers legados |
| Módulo contratado | Parcial | `tenant_modulo_contratado` existente e catálogo de recurso | P1: consolidar avaliação em filtro único |
| Rotas de governança | Implementado | Controllers API com autenticação obrigatória | P2: policies específicas por ação |
| Menu Web | Parcial seguro | Grupo autenticado Governança e Segurança | P2: ocultação item a item por permissão efetiva |
| Negativas e exportações | Modelo implementado | `seguranca_evento` e `auditoria_exportacao` | P1: instrumentar todos os exports legados |

Administradores gerais continuam cobertos pelo fluxo existente. Administrador de tenant e usuário comum devem ser avaliados por tenant, módulo contratado, restrições e concessões; a homologação de precedência integra a RC50.52.
