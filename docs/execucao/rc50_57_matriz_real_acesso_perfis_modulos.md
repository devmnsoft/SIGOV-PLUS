# RC50.57 — matriz real de acesso por perfil e módulo

A tela canônica é `/Seguranca/MatrizAcesso`. O backend calcula bloqueios; esconder menu não concede nem revoga acesso. Exportação CSV requer `seguranca.matriz.exportar` (ou SuperAdmin) e registra sucesso ou negativa com tenant, usuário, IP, user-agent e correlation id.

| Perfil | Escopo liberado | Bloqueios essenciais |
|---|---|---|
| SUPERADMIN | todos os tenants, módulos e ações | nenhum funcional; ações críticas auditadas |
| ADMIN_TENANT | usuários, perfis, permissões e configuração do próprio tenant/módulos habilitados | outro tenant e configuração global |
| GESTOR_MUNICIPAL | dashboards, relatórios e aprovações autorizadas da secretaria | administração global |
| COORDENADOR_AREA | dashboard, validação e tarefas da área | áreas alheias e financeiro global |
| OPERACIONAL | criar, consultar e editar no módulo concedido | aprovar, configurar e exportar sensível |
| FINANCEIRO | pagamentos, faturas, arrecadação e relatórios financeiros | Saúde sensível, configuração; estorno sem grant específico |
| AUDITOR | Auditoria, LGPD e relatórios em leitura | alteração operacional |
| ATENDIMENTO | protocolo, ouvidoria e e-SIC | baixa financeira e dado sensível |
| GESTOR_MODULO | parâmetros/cadastros auxiliares do módulo concedido | outros módulos |
| LEITURA | consulta concedida | criação, alteração, cancelamento e aprovação |
| CIDADAO | próprios dados e protocolos no portal | dados internos/de terceiros |

A habilitação efetiva combina autenticação, perfil, claim de módulo contratado/habilitado e permissão `modulo.recurso.acao`. Negativas de detalhe modular retornam 403 e são auditadas. Recurso ausente retorna 404.
