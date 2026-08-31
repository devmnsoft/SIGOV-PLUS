# Identidade, acesso e tenants — RC50.87

O identificador de entrada pode ser CPF ou e-mail e o ambiente pode ser resolvido por CNPJ ou domínio. A resolução deve resultar em exatamente um tenant ativo antes da autenticação. Mensagens externas são genéricas; eventos armazenam hashes do identificador e IP, CPF final mascarável e user-agent sanitizado, nunca documento, senha ou token em claro.

`identidade_usuario_documento` impõe unicidade do hash do CPF ativo por tenant e guarda o documento cifrado fora de logs. `identidade_sessao` guarda somente hash do token, expiração e revogação. Administradores do cliente precisam de CPF e e-mail verificado; o marcador MNSOFT é interno e não integra formulários do cliente.

A ordem de decisão é: contexto válido; estado do cliente; sessão/usuário ativos; plano; bloqueios globais; perfil do tenant; permissão de módulo, tela e ação. Qualquer falha nega acesso. Logout revoga a sessão persistida e remove o cookie. Recuperação de senha não revela a existência da conta.

## LGPD

Listagens exibem apenas documento mascarado. Auditorias usam IDs internos e `correlation_id`. Exportações aplicam neutralização de fórmulas CSV. Retenção, acesso e descarte devem seguir a política do controlador público e o contrato com a MNSOFT.
